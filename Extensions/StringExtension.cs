using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NokitaKaze.Base58Check;

namespace UsdtTelegrambot.Extensions
{
    public static class StringExtension
    {
        public static string ToMD5(this string value)
        {
            byte[] array;
            using (MD5 mD = MD5.Create())
            {
                array = mD.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < array.Length; i++)
            {
                stringBuilder.Append(array[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }

        public static string ToHexString(this string value)
        {
            return Convert.ToHexString(Encoding.UTF8.GetBytes(value));
        }

        public static string HexToString(this string value)
        {
            return Encoding.UTF8.GetString(Convert.FromHexString(value));
        }

        public static string DecodeBase58(this string value)
        {
            return Convert.ToHexString(Base58CheckEncoding.Decode(value));
        }

        public static byte[] FromHexString(this string hexString)
        {
            byte[] array = new byte[hexString.Length / 2];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return array;
        }

        public static string EncodeBase58(this string value)
        {
            return Base58CheckEncoding.Encode(FromHexString(value));
        }

        public static string ToHMACSHA256(this string message, string secret)
        {
            secret = secret ?? "";
            UTF8Encoding uTF8Encoding = new UTF8Encoding();
            byte[] bytes = uTF8Encoding.GetBytes(secret);
            byte[] bytes2 = uTF8Encoding.GetBytes(message);
            using HMACSHA256 hMACSHA = new HMACSHA256(bytes);
            byte[] array = hMACSHA.ComputeHash(bytes2);
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < array.Length; i++)
            {
                stringBuilder.Append(array[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }

        public static string ToCnUnit(this long value)
        {
            return ToCnUnit((decimal)value);
        }

        public static string ToCnUnit(this decimal value)
        {
            string result = $"{value * 1.0m / 10000m:0.00} 万";
            if (value > 100000000m)
            {
                result = $"{value * 1.0m / 10000m / 10000m:0.00} 亿";
            }
            else if (value == 0m)
            {
                result = "未知";
            }
            return result;
        }
    }
}
