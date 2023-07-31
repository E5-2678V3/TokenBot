using Flurl;
using Flurl.Http;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding;
using System.Collections.Generic;
using System.Globalization;
using Telegram.Bot;
using UsdtTelegrambot.BgServices.Base;
using UsdtTelegrambot.Domains.Tables;
using UsdtTelegrambot.Extensions;
using UsdtTelegrambot.Models;
using UsdtTelegrambot.Models.TronModel;

namespace UsdtTelegrambot.BgServices
{
    public class USDT_TRC20OutService : BaseScheduledService
    {
        private readonly ILogger<USDT_TRC20InService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ITelegramBotClient _botClient;
        private readonly IHostEnvironment _env;
        private readonly IServiceProvider _serviceProvider;

        public USDT_TRC20OutService(ILogger<USDT_TRC20InService> logger,
            IConfiguration configuration,
            ITelegramBotClient botClient,
            IHostEnvironment env,
            IServiceProvider serviceProvider) : base("USDT-TRC20出账记录检测", TimeSpan.FromSeconds(10), logger)
        {
            _logger = logger;
            _configuration = configuration;
            _botClient = botClient;
            _env = env;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync()
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;
            var _myTronConfig = provider.GetRequiredService<IOptionsSnapshot<MyTronConfig>>();
            var _repository = provider.GetRequiredService<IBaseRepository<TokenRecord>>();
            IHostEnvironment hostEnvironment = provider.GetRequiredService<IHostEnvironment>();
            var _bindRepository = provider.GetRequiredService<IBaseRepository<TokenBind>>();

            var payMinTime = DateTime.Now.AddSeconds(-60 * 5);
            List<string> tempList = _bindRepository.Where(x => x.Currency == Currency.USDT).ToList(x => x.Address);
            tempList = tempList.Distinct().ToList();
            var addressArray = tempList.ToArray();
            if (addressArray.Length == 0)
            {
                _logger.LogWarning("未配置USDT监控地址！");
                return;
            }
            var ContractAddress = _configuration.GetValue("TronConfig:USDTContractAddress", "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t");
            var BaseUrl = _configuration.GetValue("TronConfig:ApiHost", "https://api.trongrid.io");
            foreach (var address in addressArray)
            {
                var query = new Dictionary<string, object>();
                //query.Add("only_confirmed", true);
                query.Add("only_from", true);
                query.Add("limit", 1);
                query.Add("min_timestamp", (long)payMinTime.ToUnixTimeStamp());
                query.Add("contract_address", ContractAddress);
                var req = BaseUrl
                    .AppendPathSegment($"v1/accounts/{address}/transactions/trc20")
                    .SetQueryParams(query)
                    .WithTimeout(15);
                if (_env.IsProduction())
                    req = req.WithHeader("TRON-PRO-API-KEY", _configuration.GetValue("TronNet:ApiKey", ""));
                var result = await req
                    .GetJsonAsync<BaseResponse<Transactions>>();

                if (result.Success && result.Data?.Count > 0)
                {
                    foreach (var item in result.Data)
                    {
                        //合约地址不匹配
                        if (item.TokenInfo?.Address != ContractAddress) continue;
                        var types = new string[] { "Transfer", "TransferFrom" };
                        //收款地址相同
                        if (item.From != address || !types.Contains(item.Type)) continue;
                        //实际支付金额
                        var amount = item.Amount;
                        var record = new TokenRecord
                        {
                            BlockTransactionId = item.TransactionId,
                            FromAddress = item.From,
                            ToAddress = item.To,
                            //ContractAddress = item.TokenInfo.Address,
                            OriginalAmount = amount,
                            OriginalCurrency = Currency.USDT,
                            ConvertCurrency = Currency.TRX,
                            Status = Status.Pending,
                            ReceiveTime = item.BlockTimestamp.ToDateTime()
                        };
                        if (!await _repository.Where(x => x.BlockTransactionId == record.BlockTransactionId).AnyAsync())
                        {
                            await _repository.InsertAsync(record);
                            _logger.LogInformation("新{OriginalCurrency}出账：{@data}", record.ConvertCurrency, record);
                            var AdminUserId = _configuration.GetValue<long>("BotConfig:AdminUserId");
                            try
                            {
                                var viewUrl = $"https://shasta.tronscan.org/#/transaction/{record.BlockTransactionId}";
                                if (hostEnvironment.IsProduction())
                                {
                                    viewUrl = $"https://tronscan.org/#/transaction/{record.BlockTransactionId}";
                                }
                                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup inlineKeyboard = new(
                                    new[]
                                    {
                                            new []
                                            {
                                                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithUrl("查看交易",viewUrl),
                                            },
                                    });
                                if (amount > (decimal)0.1)
                                {
                                    List<long> Userlist = await _bindRepository.Where(x => x.Address == address).ToListAsync(x => x.UserId);
                                    var usdtbalance = await GetUSDTBalance(address);
                                    foreach (var user in Userlist)
                                    {
                                        try
                                        {
                                            await _botClient.SendTextMessageAsync(user,
                                                    $@"您有一笔<b>出账</b>交易
出账金额：<b>{record.OriginalAmount:#.######} {record.OriginalCurrency}</b>
哈希：<code>{record.BlockTransactionId}</code>
当前余额：<b>{usdtbalance}</b>
时间：<b>{record.ReceiveTime:yyyy-MM-dd HH:mm:ss}</b>
出账地址：<code>{record.FromAddress}</code>
接收地址：<code>{record.ToAddress}</code>
                                            ", Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: inlineKeyboard);
                                        }
                                        catch (Exception e)
                                        {
                                            _logger.LogError(e, $"给用户发送通知失败！用户ID：{user}");
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "发送TG通知失败！");
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("暂无支付记录");
                }
            }
        }
        async Task<decimal> GetUSDTBalance(string Address)
        {
            string text = Address.DecodeBase58();
            FunctionCallEncoder functionCallEncoder = new FunctionCallEncoder();
            Nethereum.ABI.Model.Parameter[] parameters = new Nethereum.ABI.Model.Parameter[1]
            {
        new Nethereum.ABI.Model.Parameter("address", "who")
            };
            object[] values = new string[1] { "0x" + text.Substring(2, text.Length - 2) };
            string parameter = Convert.ToHexString(functionCallEncoder.EncodeParameters(parameters, values));
            IFlurlRequest flurlRequest = "https://api.trongrid.io/wallet/triggerconstantcontract".WithTimeout(5);
            string[] array = new string[] { "TRON-PRO-API-KEY" };
            if (array.Length != 0)
            {
                string value = array.OrderBy((string x) => Guid.NewGuid()).First();
                flurlRequest = flurlRequest.WithHeader("TRON-PRO-API-KEY", _configuration.GetValue("TronNet:ApiKey", ""));
            }

            BalanceOfModel balanceOfModel = await flurlRequest.PostJsonAsync(new
            {
                owner_address = Address,
                contract_address = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t",
                function_selector = "balanceOf(address)",
                parameter = parameter,
                visible = true
            }).ReceiveJson<BalanceOfModel>();

            if (balanceOfModel.Result.Result)
            {
                string text2 = balanceOfModel.ConstantResult.FirstOrDefault();
                if (!string.IsNullOrEmpty(text2))
                {
                    text2 = text2.TrimStart('0');
                    if (long.TryParse(text2, NumberStyles.HexNumber, null, out var result))
                    {
                        return (decimal)result / 1000000m;
                    }
                }
            }
            return default(decimal);
        }
    }
}
