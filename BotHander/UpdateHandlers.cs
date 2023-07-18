using Flurl;
using Flurl.Http;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding;
using Serilog;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using UsdtTelegrambot.Domains.Tables;
using UsdtTelegrambot.Extensions;
using UsdtTelegrambot.Models;

namespace UsdtTelegrambot.BotHander
{
    public static class UpdateHandlers
    {
        public static string? BotUserName = null!;
        public static IConfiguration configuration = null!;
        public static IFreeSql freeSql = null!;
        public static IServiceScopeFactory serviceScopeFactory = null!;
        public static long AdminUserId => configuration.GetValue<long>("BotConfig:AdminUserId");
        public static string AdminUserUrl => configuration.GetValue<string>("BotConfig:AdminUserUrl");

        private static readonly string tronUsdtContractAddress = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";  //Tron USDT合约
        private static readonly string ETHUsdtContractAddress = "0xdac17f958d2ee523a2206206994597c13d831ec7";  //ETH USDT合约
        private static readonly string ETHUsdcContractAddress = "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48";//ETH USDC合约

        /// <summary>
        /// 错误处理
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="exception"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task PollingErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Log.Error(exception, ErrorMessage);
            return Task.CompletedTask;
        }
        /// <summary>
        /// 处理更新
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(botClient, update.Message!),
                _ => UnknownUpdateHandlerAsync(botClient, update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "呜呜呜，机器人输错啦~");
                await PollingErrorHandler(botClient, exception, cancellationToken);
            }
        }
        /// <summary>
        /// 消息接收
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
        {
            Log.Information($"Receive message type: {message.Type}");
            if (message.Text is not { } messageText)
                return;
            var scope = serviceScopeFactory.CreateScope();
            var provider = scope.ServiceProvider;

            ///接收群组消息
            if (message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup)
            {
                var groupId = message.Chat.Id;
             ;

                //判断是否TRON链
                if (message.Text.StartsWith("T"))
                {
                    // 提取 USDT 地址
                 Task.Run(async () =>await ExtractGroupchatTRONAddresses(botClient, message, groupId));
                }
                //判断获取为ETH链
                else if (message.Text.StartsWith("0x")) 
                {
                    Task.Run(async () => await ExtractGroupchatETHAddresses(botClient, message, groupId));
                }
                else if (message.Text.StartsWith("汇率"))
                {
                    var _rateRepository = provider.GetRequiredService<IBaseRepository<TokenRate>>();
                    decimal rate = await _rateRepository.Where(x => x.Currency == "USDT" && x.FiatCurrency == FiatCurrency.CNY).FirstAsync(x => x.Rate);
                    var msg = @$"今日汇率：
USDT/CNY汇率： <code>{rate}</code>
                            ";
                    var sendmessagereturn = await botClient.SendTextMessageAsync(chatId: groupId, text: msg, parseMode: ParseMode.Html, disableWebPagePreview: true, replyToMessageId: message.MessageId);
                }
            }
            ///接收私人会话消息
            else
            {
                var _myTronConfig = provider.GetRequiredService<IOptionsSnapshot<MyTronConfig>>();
                try
                {
                    await InsertOrUpdateUserAsync(botClient, message);
                }
                catch (Exception e)
                {
                    Log.Logger.Error(e, "更新Telegram用户信息失败！");
                }
                messageText = messageText.Replace($"@{BotUserName}", "");
                var action = messageText.Split(' ')[0] switch
                {
                    "/start" => Start(botClient, message),
                    "绑定USDT地址" => BindAddress(botClient, message),
                    "解绑USDT地址" => UnBindAddress(botClient, message),
                    "查询USDT地址" => CheckBindAddress(botClient, message),
                    _ => Usage(botClient, message)
                };
                Message sentMessage = await action;
            }

            async Task ExtractGroupchatTRONAddresses(ITelegramBotClient botClient, Message message,long  groupId)
            {
                // 在这里实现提取文本中的 USDT 地址的逻辑
                // 返回一个包含提取到的地址的列表
                // 示例代码：
                var addresses = new List<string>();
                var words = message.Text.Split(' ');

                foreach (var word in words)
                {
                    if (word.StartsWith("T") && word.Length == 34)
                    {
                        addresses.Add(word);
                    }
                }
                if (addresses.Any())
                {
                    foreach (var address in addresses)
                    {
                        var tradeSum = await GetTradeSum(address, tronUsdtContractAddress);
                        var usdtBalance = await GetUSDTBalance(address, tronUsdtContractAddress);
                        var addresstime = await GetAddressTime(address);
                        var recenttradehistory = await GetTradeHistory(address, tronUsdtContractAddress);
                        var msg = @$"当前账户查询如下：
地址类型：TRON 波场链
地址： <code>{address}</code>

—————————资源—————————
USDT总交易笔数：<b> {tradeSum}</b>
TRX余额：   <b>{addresstime.Split("|")[2].ToString()}</b>
USDT余额：<b>{usdtBalance}</b>
激活时间：  <b>{addresstime.Split("|")[0].ToString()}</b>
活跃时间：  <b>{addresstime.Split("|")[1].ToString()}</b>

———————最近交易记录———————
{recenttradehistory} ";
                        var sendmessagereturn = await botClient.SendTextMessageAsync(chatId: groupId, text: msg, parseMode: ParseMode.Html, disableWebPagePreview: true, replyToMessageId: message.MessageId);
                        await Task.Delay(30000);
                        await botClient.DeleteMessageAsync(chatId: groupId, messageId: message.MessageId);
                        await Task.Delay(1000);
                        await botClient.DeleteMessageAsync(chatId: groupId, messageId: sendmessagereturn.MessageId);
                    }
                }

             
            }

            async Task ExtractGroupchatETHAddresses(ITelegramBotClient botClient, Message message, long groupId)
            {
                // 在这里实现提取文本中的 USDT 地址的逻辑
                // 返回一个包含提取到的地址的列表
                // 示例代码：
                var addresses = new List<string>();
                var words = message.Text.Split(' ');
                string regex = "^0x[0-9a-fA-F]{40}$";

                foreach (var word in words)
                {
                    if (word.StartsWith("0x") && Regex.IsMatch(word, regex, RegexOptions.IgnoreCase))
                    {
                        addresses.Add(word);
                    }
                }
                if (addresses.Any())
                {
                    foreach (var address in addresses)
                    {
                        var ethinfo = await GetETHaddressinfo(address);
                        if (ethinfo != null)
                        {
                            var creattime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(ethinfo.firstTransactionTime)).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                            var lasttime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(ethinfo.lastTransactionTime)).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                            var ethtradesum = ethinfo.transactionCount;
                            var ethbalance = ethinfo.balance;
                            var sendeth = ethinfo.sendAmount;
                            var receiveth = ethinfo.receiveAmount;
                            var usdterc20balance = await GetETHUSDTbalance(address, ETHUsdtContractAddress);
                            var usdcerc20balance = await GetETHUSDCbalance(address, ETHUsdcContractAddress);
                            var msg = @$"当前账户查询如下：
地址类型：ETH 以太坊
地址： <code>{address}</code>

—————————资源—————————
总交易笔数：<b>{ethtradesum}</b>
ETH余额：<b>{ethbalance}</b>
总接收ETH:<b>{receiveth}</b>
总发送ETH:<b>{sendeth}</b>
USDT余额：<b>{usdterc20balance}</b>
USDC余额：<b>{usdcerc20balance}</b>
激活时间：  <b>{creattime}</b>
活跃时间：  <b>{lasttime}</b>";
                            var sendmessagereturn = await botClient.SendTextMessageAsync(chatId: groupId, text: msg, parseMode: ParseMode.Html, disableWebPagePreview: true, replyToMessageId: message.MessageId);
                            await Task.Delay(30000);
                            await botClient.DeleteMessageAsync(chatId: groupId, messageId: message.MessageId);
                            await Task.Delay(1000);
                            await botClient.DeleteMessageAsync(chatId: groupId, messageId: sendmessagereturn.MessageId);
                        }
                        else
                        {
                            var msg = @$"哥们儿哥们儿，你查的ETH查不出来哥们儿";
                            var sendmessagereturn = await botClient.SendTextMessageAsync(chatId: groupId, text: msg, parseMode: ParseMode.Html, disableWebPagePreview: true, replyToMessageId: message.MessageId);
                            await Task.Delay(30000);
                            await botClient.DeleteMessageAsync(chatId: groupId, messageId: message.MessageId);
                            await Task.Delay(1000);
                            await botClient.DeleteMessageAsync(chatId: groupId, messageId: sendmessagereturn.MessageId);
                        }
                    }
                }
            }


            static async Task<decimal> GetUSDTBalance(string Address, string contractAddress)
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
                    flurlRequest = flurlRequest.WithHeader("TRON-PRO-API-KEY", configuration.GetValue("TronNet:ApiKey", ""));
                }

                BalanceOfModel balanceOfModel = await flurlRequest.PostJsonAsync(new
                {
                    owner_address = Address,
                    contract_address = contractAddress,
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

            static async Task<string> GetTradeSum(string Address, string contractAddress)
            {
                var query = new Dictionary<string, object>();
                query.Add("limit", 1);
                query.Add("chainShortName", "TRON");
                query.Add("address", Address);
                query.Add("protocolType", "token_20");
                query.Add("tokenContractAddress", contractAddress);
                var req = "https://www.oklink.com/".AppendPathSegment($"/api/v5/explorer/address/transaction-list").SetQueryParams(query).WithTimeout(15);
                req = req.WithHeader("OK-ACCESS-KEY", configuration.GetValue("OKLINK:Key", ""));
                var result = await req
                    .GetJsonAsync<Models.ApiResponse>();
                if (result.data.Length > 0)
                {
                    return result.data[0].totalPage;
                }
                else { return "0"; }
            }

            static async Task<string> GetTradeHistory(string Address, string contractAddress)
            {
                StringBuilder content = new StringBuilder();
                var query = new Dictionary<string, object>();
                query.Add("limit", 5);
                query.Add("chainShortName", "TRON");
                query.Add("address", Address);
                query.Add("protocolType", "token_20");
                query.Add("tokenContractAddress", contractAddress);
                var req = "https://www.oklink.com/".AppendPathSegment($"/api/v5/explorer/address/transaction-list").SetQueryParams(query)  .WithTimeout(15);
                req = req.WithHeader("OK-ACCESS-KEY", configuration.GetValue("OKLINK:Key", ""));
                var result = await req
                    .GetJsonAsync<Models.ApiResponse>();
                if (result.data.Length > 0)
                {
                    if (result.data[0].transactionLists.Length > 0)
                    {
                        foreach (var item in result.data[0].transactionLists)
                        {
                            if (item.to == Address)
                            {
                                var time = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item.transactionTime)).UtcDateTime;
                                content.AppendLine($"入账: 交易时间 <b>{time.ToString("yyyy-MM-dd HH:mm:ss")}</b>  交易金额:<b>{item.amount}</b>  <a href=\"https://tronscan.org/#/transaction/{item.txId}\">查看交易</a> ");
                            }
                            else
                            {
                                var time = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item.transactionTime)).UtcDateTime;
                                content.AppendLine($"出账: 交易时间 <b>{time.ToString("yyyy-MM-dd HH:mm:ss")}</b>  交易金额:<b>{item.amount}</b>  <a href=\"https://tronscan.org/#/transaction/{item.txId}\">查看交易</a>");
                            }
                        }
                    }
                    else { content.AppendLine("该地址无交易数据"); }
                    return content.ToString();
                }
                else { return "Error"; }
            }

            static async Task<ETHresponseDataItem?> GetETHaddressinfo(string Address)
            {
                var query = new Dictionary<string, object>();
                query.Add("chainShortName", "ETH");
                 query.Add("address", Address);
                var req = "https://www.oklink.com/".AppendPathSegment($"/api/v5/explorer/address/address-summary").SetQueryParams(query).WithTimeout(15);
                req = req.WithHeader("OK-ACCESS-KEY", configuration.GetValue("OKLINK:Key", ""));
                var result = await req
                    .GetJsonAsync<ETHresponse>();
               
                if (result.data.Count > 0)
                {
                    return result.data.First();
                }
                else { return null; }
          
            }

            static async Task<string?> GetETHUSDTbalance(string Address, string contractAddress)
            {
                var query = new Dictionary<string, object>();
                query.Add("chainShortName", "ETH");
                query.Add("protocolType", "token_20");
                query.Add("tokenContractAddress", contractAddress);
                query.Add("address", Address);
                var req = "https://www.oklink.com/".AppendPathSegment($"/api/v5/explorer/address/address-balance-fills").SetQueryParams(query).WithTimeout(15);
                req = req.WithHeader("OK-ACCESS-KEY", configuration.GetValue("OKLINK:Key", "")); 
                var result = await req
                    .GetJsonAsync<ETHTRC20Response>();

                if (result.data.Count > 0 && result.data.First().tokenList.Count > 0)
                {
                   return result.data.First().tokenList.First().holdingAmount;
                }
                else { return "0"; }
            }
            
            static async Task<string?> GetETHUSDCbalance(string Address, string contractAddress)
            {
                var query = new Dictionary<string, object>();
                query.Add("chainShortName", "ETH");
                query.Add("protocolType", "token_20");
                query.Add("tokenContractAddress", contractAddress);
                query.Add("address", Address);
                var req = "https://www.oklink.com/".AppendPathSegment($"/api/v5/explorer/address/address-balance-fills").SetQueryParams(query).WithTimeout(15);
                req = req.WithHeader("OK-ACCESS-KEY", configuration.GetValue("OKLINK:Key", ""));
                var result = await req
                    .GetJsonAsync<ETHTRC20Response>();

                if (result.data.Count > 0 && result.data.First().tokenList.Count>0)
                {
                    return result.data.First().tokenList.First().holdingAmount;
                }
                else { return "0"; }
            }

            async Task<string> GetAddressTime(string Address)
            {

                IFlurlRequest flurlRequest = "https://api.trongrid.io/wallet/getaccount".WithTimeout(5);
                string[] array = new string[] { "" };
                if (array.Length != 0)
                {
                    string value = array.OrderBy((string x) => Guid.NewGuid()).First();
                    flurlRequest = flurlRequest.WithHeader("TRON-PRO-API-KEY", configuration.GetValue("TronNet:ApiKey", ""));
                }

                GetAccountModel accountModel = await flurlRequest.PostJsonAsync(new
                {
                    address = Address,
                    visible = true
                }).ReceiveJson<GetAccountModel>();
                var creattime = DateTimeOffset.FromUnixTimeMilliseconds(accountModel.CreateTime).UtcDateTime;
                var lasttradetime = DateTimeOffset.FromUnixTimeMilliseconds(accountModel.LatestOprationTime).UtcDateTime;
                return creattime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + lasttradetime.ToString("yyyy-MM-dd HH:mm:ss") + "|" +(accountModel.Balance/ 1000000m).ToString();
            }

            async Task<Message> BindAddress(ITelegramBotClient botClient, Message message)
            {
                if (message.From == null) return message;
                if (message.Text is not { } messageText)
                    return message;
                var address = messageText.Split(' ').Last();
                if (address.StartsWith("T") && address.Length == 34)
                {
                    var from = message.From;
                    var UserId = message.Chat.Id;

                    var _bindRepository = provider.GetRequiredService<IBaseRepository<TokenBind>>();
                    var bind = await _bindRepository.Where(x => x.UserId == UserId && x.Address == address).FirstAsync();
                    if (bind == null)
                    {
                        bind = new TokenBind();
                        bind.Currency = Currency.USDT;
                        bind.UserId = UserId;
                        bind.Address = address;
                        bind.UserName = $"@{from.Username}";
                        bind.FullName = $"{from.FirstName} {from.LastName}";
                        await _bindRepository.InsertAsync(bind);
                    }
                    else
                    {
                        bind.Currency = Currency.USDT;
                        bind.UserId = UserId;
                        bind.Address = address;
                        bind.UserName = $"@{from.Username}";
                        bind.FullName = $"{from.FirstName} {from.LastName}";
                        await _bindRepository.UpdateAsync(bind);
                    }
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: @$"您已成功绑定<b>{address}</b>！
当您的钱包存在时，您将收到通知！
如需解绑，请发送
<code>解绑USDT地址 Txxxxxxx</code>(您的钱包地址)", parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
                }
                else
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"您输入的USDT-Trc20地址<b>{address}</b>有误！", parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
                }
            }

            async Task<Message> UnBindAddress(ITelegramBotClient botClient, Message message)
            {
                if (message.From == null) return message;
                if (message.Text is not { } messageText)
                    return message;
                var address = messageText.Split(' ').Last();

                var _bindRepository = provider.GetRequiredService<IBaseRepository<TokenBind>>();
                var from = message.From;
                var UserId = message.Chat.Id;
                var bind = await _bindRepository.Where(x => x.UserId == UserId && x.Address == address).FirstAsync();
                if (bind != null)
                {
                    await _bindRepository.DeleteAsync(bind);
                }
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"您已成功解绑<b>{address}</b>！", parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());

            }

            async Task<Message> CheckBindAddress(ITelegramBotClient botClient, Message message)
            {
                if (message.From == null) return message;
                if (message.Text is not { } messageText)
                    return message;
                var _bindRepository = provider.GetRequiredService<IBaseRepository<TokenBind>>();
                var from = message.From;
                var UserId = message.Chat.Id;
                var bind = await _bindRepository.Where(x => x.UserId == UserId).ToListAsync(x=>x.Address);
                StringBuilder sb = new StringBuilder();
                if (bind.Count > 0)
                {
                    foreach (var item in bind)
                    {
                        sb.AppendLine("<b>"+item+"</b>");
                    }
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"{sb}", parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
                }
                else 
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"当前绑定地址为空！", parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
                }
            }




            //通用回复
            static async Task<Message> Start(ITelegramBotClient botClient, Message message)
            {
                string usage = @$"USDT地址监控机器人！
私人功能(直接使用机器人)：
添加USDT监控地址:<code>绑定USDT地址 你的USDT地址</code>
解绑USDT监控地址:<code>解绑USDT地址 你的USDT地址</code>
查询绑定的USDT监控地址:<code>查询USDT地址</code>

群组功能(需添加机器人到群组)：
查询余额：输入USDT-TRC20地址，机器人将自动查询地址余额
汇率：群组中输入汇率，机器人将自动发送汇率
---------------------------------------------------------------------
如有需要，请联系管理员： {AdminUserUrl}
                    ";
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: usage,
                                                            parseMode: ParseMode.Html,
                                                            disableWebPagePreview: true,
                                                            replyMarkup: new ReplyKeyboardRemove());
            }
            //估价

            //通用回复
            static async Task<Message> Usage(ITelegramBotClient botClient, Message message)
            {
                var text = (message.Text ?? "").ToUpper().Trim();
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                  text: "祝老板天天发大财，快乐每一天",
                                                                  parseMode: ParseMode.Html,
                                                                  replyMarkup: new ReplyKeyboardRemove());

            }




            async Task InsertOrUpdateUserAsync(ITelegramBotClient botClient, Message message)
            {
                if (message.From == null) return;
                var curd = provider.GetRequiredService<IBaseRepository<Users>>();
                var from = message.From;
                var UserId = message.Chat.Id;
                Log.Information("{user}: {message}", $"{from.FirstName} {from.LastName}", message.Text);

                var user = await curd.Where(x => x.UserId == UserId).FirstAsync();
                if (user == null)
                {
                    user = new Users
                    {
                        UserId = UserId,
                        UserName = from.Username,
                        FirstName = from.FirstName,
                        LastName = from.LastName
                    };
                    await curd.InsertAsync(user);
                    return;
                }
                user.UserId = UserId;
                user.UserName = from.Username;
                user.FirstName = from.FirstName;
                user.LastName = from.LastName;
                await curd.UpdateAsync(user);
            }
        }

        private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Log.Information($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }
    }
}
