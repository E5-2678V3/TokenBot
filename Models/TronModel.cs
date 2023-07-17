using Nethereum.ABI.FunctionEncoding;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Globalization;
using UsdtTelegrambot.Extensions;

namespace UsdtTelegrambot.Models
{


    public enum AbiFunction
    {
        [Description("转账")]
        transfer = 10,
        [Description("授权转账")]
        transferFrom,
        [Description("授权")]
        approve,
        [Description("增加授权额度")]
        increaseApproval,
        [Description("减少授权额度")]
        decreaseApproval,
        [Description("TRX 闪兑 USDT")]
        trxToTokenSwapInput,
        [Description("USDT 闪兑 TRX")]
        tokenToTrxSwapInput,
        [Description("批量转账")]
        FillOrderStraight
    }

    public class AccountResource
    {
        [JsonProperty("energy_usage")]
        public long EnergyUsage { get; set; }

        [JsonProperty("frozen_balance_for_energy")]
        public Frozen FrozenBalanceForEnergy { get; set; }

        [JsonProperty("latest_consume_time_for_energy")]
        public long LatestConsumeTimeForEnergy { get; set; }
    }

    public class ActivePermission
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("permission_name")]
        public string PermissionName { get; set; }

        [JsonProperty("threshold")]
        public long Threshold { get; set; }

        [JsonProperty("operations")]
        public string Operations { get; set; }

        public ContractType[] OperationEnums
        {
            get
            {
                List<ContractType> list = new List<ContractType>();
                string text = Convert.ToString(long.Parse(Operations.TrimEnd('0'), NumberStyles.HexNumber), 2);
                for (int i = 0; i < text.Length; i++)
                {
                    _ = text[i];
                }
                return list.ToArray();
            }
        }

        [JsonProperty("keys")]
        public Key[] Keys { get; set; }
    }

    public class BalanceOfModel
    {
        [JsonProperty("result")]
        public ResultData? Result { get; set; }

        [JsonProperty("energy_used")]
        public int EnergyUsed { get; set; }

        [JsonProperty("constant_result")]
        public List<string>? ConstantResult { get; set; }

        [JsonProperty("transaction")]
        public Transaction? Transaction { get; set; }
    }
    public class BlockHeader
    {
        [JsonProperty("raw_data")]
        public BlockHeaderRawData RawData { get; set; }

        [JsonProperty("witness_signature")]
        public string WitnessSignature { get; set; }
    }
    public class BlockHeaderRawData
    {
        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("txTrieRoot")]
        public string TxTrieRoot { get; set; }

        [JsonProperty("witness_address")]
        public string WitnessAddress { get; set; }

        [JsonProperty("parentHash")]
        public string ParentHash { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }
    }

    public class BlockResult
    {
        [JsonProperty("block")]
        public List<BlockRoot> Block { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class BlockRoot
    {
        [JsonProperty("blockID")]
        public string BlockID { get; set; }

        [JsonProperty("block_header")]
        public BlockHeader BlockHeader { get; set; }

        [JsonProperty("transactions")]
        public List<Transaction> Transactions { get; set; }
    }

    public class Contract
    {
        [JsonProperty("parameter")]
        public Parameter Parameter { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("Permission_id")]
        public int PermissionId { get; set; }
    }


    public enum ContractType
    {
        [Description("账号创建")]
        AccountCreateContract = 0,
        [Description("TRX转账")]
        TransferContract = 1,
        [Description("TRC10转账")]
        TransferAssetContract = 2,
        [Description("投票")]
        VoteAssetContract = 3,
        VoteWitnessContract = 4,
        WitnessCreateContract = 5,
        AssetIssueContract = 6,
        WitnessUpdateContract = 8,
        ParticipateAssetIssueContract = 9,
        AccountUpdateContract = 10,
        FreezeBalanceContract = 11,
        UnfreezeBalanceContract = 12,
        WithdrawBalanceContract = 13,
        UnfreezeAssetContract = 14,
        UpdateAssetContract = 15,
        ProposalCreateContract = 16,
        ProposalApproveContract = 17,
        ProposalDeleteContract = 18,
        SetAccountIdContract = 19,
        CustomContract = 20,
        CreateSmartContract = 30,
        [Description("TRC20/TRC721/TRC1155转账")]
        TriggerSmartContract = 31,
        GetContract = 32,
        UpdateSettingContract = 33,
        ExchangeCreateContract = 41,
        ExchangeInjectContract = 42,
        ExchangeWithdrawContract = 43,
        ExchangeTransactionContract = 44,
        UpdateEnergyLimitContract = 45,
        AccountPermissionUpdateContract = 46,
        ClearABIContract = 48,
        UpdateBrokerageContract = 49,
        ShieldedTransferContract = 51,
        MarketSellAssetContract = 52,
        MarketCancelOrderContract = 53
    }



    public class Frozen
    {
        [JsonProperty("frozen_balance")]
        public long FrozenBalance { get; set; }

        [JsonProperty("expire_time")]
        public long ExpireTime { get; set; }
    }


    public class GetAccountModel
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("balance")]
        public long Balance { get; set; }

        [JsonProperty("votes")]
        public Vote[] Votes { get; set; }

        [JsonProperty("frozen")]
        public Frozen[] Frozen { get; set; }

        [JsonProperty("net_usage")]
        public long NetUsage { get; set; }

        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("latest_opration_time")]
        public long LatestOprationTime { get; set; }

        [JsonProperty("free_net_usage")]
        public long FreeNetUsage { get; set; }

        [JsonProperty("latest_consume_time")]
        public long LatestConsumeTime { get; set; }

        [JsonProperty("latest_consume_free_time")]
        public long LatestConsumeFreeTime { get; set; }

        [JsonProperty("account_resource")]
        public AccountResource AccountResource { get; set; }

        [JsonProperty("owner_permission")]
        public OwnerPermission OwnerPermission { get; set; }

        [JsonProperty("active_permission")]
        public ActivePermission[] ActivePermission { get; set; }

        [JsonProperty("asset_optimized")]
        public bool AssetOptimized { get; set; }
    }

    public class GetAccountResourceModel
    {
        [JsonProperty("freeNetUsed")]
        public long FreeNetUsed { get; set; }

        [JsonProperty("freeNetLimit")]
        public long FreeNetLimit { get; set; }

        [JsonProperty("NetUsed")]
        public long NetUsed { get; set; }

        [JsonProperty("NetLimit")]
        public long NetLimit { get; set; }

        [JsonProperty("TotalNetLimit")]
        public long TotalNetLimit { get; set; }

        [JsonProperty("TotalNetWeight")]
        public long TotalNetWeight { get; set; }

        [JsonProperty("tronPowerUsed")]
        public long TronPowerUsed { get; set; }

        [JsonProperty("tronPowerLimit")]
        public long TronPowerLimit { get; set; }

        [JsonProperty("EnergyUsed")]
        public long EnergyUsed { get; set; }

        [JsonProperty("EnergyLimit")]
        public long EnergyLimit { get; set; }

        [JsonProperty("TotalEnergyLimit")]
        public long TotalEnergyLimit { get; set; }

        [JsonProperty("TotalEnergyWeight")]
        public long TotalEnergyWeight { get; set; }
    }


    public class Key
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("weight")]
        public long Weight { get; set; }
    }
    public class OwnerPermission
    {
        [JsonProperty("permission_name")]
        public string PermissionName { get; set; }

        [JsonProperty("threshold")]
        public long Threshold { get; set; }

        [JsonProperty("keys")]
        public Key[] Keys { get; set; }
    }


    public class Parameter
    {
        [JsonProperty("value")]
        public Value Value { get; set; }

        [JsonProperty("type_url")]
        public string TypeUrl { get; set; }
    }



    public class ResultData
    {
        [JsonProperty("result")]
        public bool Result { get; set; }
    }


    public class RetData
    {
        [JsonProperty("contractRet")]
        public string ContractRet { get; set; }
    }

    public class Transaction
    {
        [JsonProperty("ret")]
        public List<RetData> Ret { get; set; }

        [JsonProperty("signature")]
        public List<string> Signature { get; set; }

        [JsonProperty("txID")]
        public string TxID { get; set; }

        [JsonProperty("raw_data")]
        public TransactionRawData RawData { get; set; }

        [JsonProperty("raw_data_hex")]
        public string RawDataHex { get; set; }
    }

    public class TransactionRawData
    {
        [JsonProperty("data")]
        public string? Data { get; set; }

        public string? DataText => Data?.HexToString();

        [JsonProperty("contract")]
        public List<Contract> Contract { get; set; }

        [JsonProperty("ref_block_bytes")]
        public string RefBlockBytes { get; set; }

        [JsonProperty("ref_block_hash")]
        public string RefBlockHash { get; set; }

        [JsonProperty("expiration")]
        public long Expiration { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }
    }

    public class Value
    {
        private static Dictionary<string, AbiFunction> FuncDic = new Dictionary<string, AbiFunction>
    {
        {
            "a9059cbb",
            AbiFunction.transfer
        },
        {
            "23b872dd",
            AbiFunction.transferFrom
        },
        {
            "095ea7b3",
            AbiFunction.approve
        },
        {
            "d73dd623",
            AbiFunction.increaseApproval
        },
        {
            "66188463",
            AbiFunction.decreaseApproval
        },
        {
            "4bf3e2d0",
            AbiFunction.trxToTokenSwapInput
        },
        {
            "999bb7ac",
            AbiFunction.tokenToTrxSwapInput
        },
        {
            "35cd050b",
            AbiFunction.FillOrderStraight
        }
    };

        public static FunctionCallDecoder functionCallDecoder = new FunctionCallDecoder();

        [JsonProperty("amount")]
        public decimal AmountRaw { get; set; }

        public decimal Amount => AmountRaw / 1000000m;

        [JsonProperty("call_value")]
        public decimal CallValueRaw { get; set; }

        public decimal CallValue => CallValueRaw / 1000000m;

        [JsonProperty("data")]
        public string? Data { get; set; }

        public AbiFunction? Function
        {
            get
            {
                if (sha3Signature == null || Data == null)
                {
                    return null;
                }
                if (FuncDic.ContainsKey(sha3Signature))
                {
                    return FuncDic[sha3Signature];
                }
                return null;
            }
        }

        public string? sha3Signature
        {
            get
            {
                if (Data == null)
                {
                    return null;
                }
                return Data.Substring(0, 8);
            }
        }

        public string? AbiData
        {
            get
            {
                if (Data == null)
                {
                    return null;
                }
                string data = Data;
                return data.Substring(8, data.Length - 8);
            }
        }

        [JsonProperty("asset_name")]
        public string AssetName { get; set; }

        [JsonProperty("owner_address")]
        public string OwnerAddress { get; set; }

        [JsonProperty("to_address")]
        public string ToAddress { get; set; }

        [JsonProperty("contract_address")]
        public string ContractAddress { get; set; }

        [JsonProperty("resource")]
        public string Resource { get; set; }

        [JsonProperty("receiver_address")]
        public string ReceiverAddress { get; set; }

        [JsonProperty("frozen_duration")]
        public int FrozenDuration { get; set; }

        [JsonProperty("frozen_balance")]
        public long FrozenBalance { get; set; }

        public decimal FrozenBalanceAmount => (decimal)FrozenBalance / 1000000m;

        [JsonProperty("balance")]
        public long Balance { get; set; }

        [JsonProperty("lock")]
        public bool Lock { get; set; }

        public T? DecodeFunctionInput<T>() where T : new()
        {
            if (Data == null || !Function.HasValue)
            {
                return default(T);
            }
            return functionCallDecoder.DecodeFunctionInput<T>(sha3Signature, AbiData);
        }
    }


    public class Vote
    {
        [JsonProperty("vote_address")]
        public string VoteAddress { get; set; }

        [JsonProperty("vote_count")]
        public long VoteCount { get; set; }
    }
}
