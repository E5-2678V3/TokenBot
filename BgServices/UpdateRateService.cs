using Flurl.Http;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsdtTelegrambot.BgServices.Base;
using UsdtTelegrambot.Domains.Tables;
using UsdtTelegrambot.Helper;

namespace UsdtTelegrambot.BgServices
{
    public class UpdateRateService : BaseScheduledService
    {
        const string baseUrl = "https://www.okx.com";
        const string User_Agent = "TokenPay/1.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36";
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
       
        private readonly ILogger<UpdateRateService> _logger;
        private readonly FlurlClient client;
        private FiatCurrency BaseCurrency => Enum.Parse<FiatCurrency>(_configuration.GetValue("BaseCurrency", "CNY"));
        public UpdateRateService(
            IConfiguration configuration,
            IServiceProvider serviceProvider,
       
            ILogger<UpdateRateService> logger) : base("更新汇率", TimeSpan.FromSeconds(3600), logger)
        {
            this._configuration = configuration;
            this._serviceProvider = serviceProvider;
            this._logger = logger;
            var WebProxy = configuration.GetValue<string>("WebProxy");
            client = new FlurlClient();
            client.Settings.Timeout = TimeSpan.FromSeconds(5);
            if (!string.IsNullOrEmpty(WebProxy))
            {
                client.Settings.HttpClientFactory = new ProxyHttpClientFactory(WebProxy);
            }

        }

        protected override async Task ExecuteAsync()
        {
       
            _logger.LogInformation("------------------{tips}------------------", "开始更新汇率");
            using IServiceScope scope = _serviceProvider.CreateScope();
            var _repository = scope.ServiceProvider.GetRequiredService<IBaseRepository<TokenRate>>();
            var list = new List<TokenRate>();
                var side = "buy";
                try
                {
                    var result = await baseUrl
                        .WithClient(client)
                        .WithHeaders(new { User_Agent = User_Agent })
                        .AppendPathSegment("/v3/c2c/otc-ticker/quotedPrice")
                        .SetQueryParams(new
                        {
                            side = side,
                            quoteCurrency = BaseCurrency.ToString(),
                            baseCurrency = "USDT",
                        })
                        .GetJsonAsync<Root>();
                    if (result.code == 0)
                    {
                        list.Add(new TokenRate
                        {
                            Id = $"USDT_{BaseCurrency}",
                            Currency = "USDT",
                            FiatCurrency = BaseCurrency,
                            LastUpdateTime = DateTime.Now,
                            Rate = result.data.First(x => x.bestOption).price,
                        });
                    }
                    else
                    {
                        _logger.LogWarning("{item} 汇率获取失败！错误信息：{msg}", "USDT", result.msg ?? result.error_message);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("{item} 汇率获取失败！错误信息：{msg}", "USDT", e?.InnerException?.Message + "; " + e?.Message);
                }
            


            foreach (var item in list)
            {
                _logger.LogInformation("更新汇率，{a}=>{b} = {c}", item.Currency, item.FiatCurrency, item.Rate);
                await _repository.InsertOrUpdateAsync(item);
            }
            _logger.LogInformation("------------------{tips}------------------", "结束更新汇率");
        }
    }

    class Datum
    {
        public bool bestOption { get; set; }
        public string payment { get; set; }
        public decimal price { get; set; }
    }

    class Root
    {
        public int code { get; set; }
        public List<Datum> data { get; set; }
        public string detailMsg { get; set; }
        public string error_code { get; set; }
        public string error_message { get; set; }
        public string msg { get; set; }
    }

    enum OkxSide
    {
        Buy,
        Sell
    }
}
