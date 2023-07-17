using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using UsdtTelegrambot.BgServices.Base;
using UsdtTelegrambot.BotHander;
using Microsoft.Extensions.DependencyInjection;

namespace UsdtTelegrambot.BgServices
{
    public class BotService : MyBackgroundService
    {
        private readonly ITelegramBotClient _client;
        private readonly IFreeSql _freeSql;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public BotService(ITelegramBotClient client,
            IFreeSql freeSql,
            IConfiguration configuration,
            IServiceScopeFactory serviceScopeFactory)
        {
            _client = client;
            _freeSql = freeSql;
            _configuration = configuration;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                ThrowPendingUpdates = true,
            };
            UpdateHandlers.freeSql = _freeSql;
            UpdateHandlers.configuration = _configuration;
            UpdateHandlers.serviceScopeFactory = _serviceScopeFactory;
            _client.StartReceiving(updateHandler: UpdateHandlers.HandleUpdateAsync,
                   pollingErrorHandler: UpdateHandlers.PollingErrorHandler,
                   receiverOptions: receiverOptions,
                   cancellationToken: stoppingToken);
            return Task.CompletedTask;
        }
    }
}
