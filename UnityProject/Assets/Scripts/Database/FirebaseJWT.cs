using UnityEngine;
using UnityEngine.Networking;
using Jose;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DatabaseAPI
{
	/// <summary>
	/// This script processes verification of Tokens for authentication on the server
	/// Not server specific, could validate tokens everywhere
	/// Works without FirebaseAPI
	///
	/// Source/Inspiration:
	/// https://github.com/FriendsOfSpatial/FirebaseAuth
	/// </summary>
	public partial class ServerData
    {
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        const string ProjectId = "expedition13";
        const string URL = "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com";

        static Dictionary<string, RSACryptoServiceProvider> cryptoServiceProviders;
        static TimeSpan timeUntilRefresh;

        public static bool Verify(string token, out string userID)
        {
            var headers = JWT.Headers<Dictionary<string, string>>(token);
            var kid = headers["kid"];

            if (cryptoServiceProviders.TryGetValue(kid, out var key))
            {
                string unparsedPayload;

                try
                {
                    unparsedPayload = JWT.Decode(token, key, JwsAlgorithm.RS256);
                }
                catch (InvalidAlgorithmException)
                {
                    userID = "INVALID ALGORITHM";
                    return false;
                }
                catch (IntegrityException)
                {
                    userID = "INVALID TOKEN";
                    return false;
                }

                var payload = JsonConvert.DeserializeObject<TokenPayload>(unparsedPayload);

                var now = ToUnixTime(DateTime.Now);

                //Must be in the future
                if (payload.exp <= now)
                {
                    userID = "TOKEN_EXPIRED";
                    return false;
                }

                //Must be in the past
                if (payload.auth_time >= now)
                {
                    userID = "INVALID AUTHENTICATION TIME";
                    return false;
                }

                //Must be in the past
                if (payload.iat >= now)
                {
                    userID = "INVALID ISSUE-AT-TIME";
                    return false;
                }

                //Must correspond to projectId
                if (payload.aud != ProjectId)
                {
                    userID = "INVALID AUDIENCE";
                    return false;
                }

                if (payload.iss != "https://securetoken.google.com/" + ProjectId)
                {
                    userID = "INVALID ISSUER";
                    return false;
                }

                userID = payload.sub;
                return true;
            }

            userID = "INVALID TOKEN KID";

            return false;
        }

        internal static async Task PeriodicKeyUpdate()
        {
            while (true)
            {
                var keys = await GetPublicKeysAsync();

                UpdateCryptoServiceProviders(keys);

                await Task.Delay(timeUntilRefresh);
            }
        }

        static async Task<string> GetPublicKeysAsync()
        {
            Uri uri = new Uri(URL);

            WebRequest webRequest = WebRequest.Create(uri);

            using (WebResponse webResponse = await webRequest.GetResponseAsync())
            {
                var headers = webResponse.Headers;
                var cacheControl = headers.Get("Cache-Control");
                var resultString = Regex.Match(cacheControl, @"\d+").Value;
                var maxAge = Int32.Parse(resultString);

                timeUntilRefresh = new TimeSpan(0, 0, maxAge);

                using (var stream = webResponse.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
            }
        }

        static void UpdateCryptoServiceProviders(string json)
        {
            var publicKeys = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            var newCryptoServiceProviders = new Dictionary<string, RSACryptoServiceProvider>();
            foreach (var keyIdPEMPair in publicKeys)
            {
                var keyId = keyIdPEMPair.Key;
                var pem = keyIdPEMPair.Value;

                var certBuffer = GetBytesFromPEM(pem);
                var publicKey = new X509Certificate2(certBuffer).PublicKey;
                var cryptoServiceProvider = publicKey.Key as RSACryptoServiceProvider;

                newCryptoServiceProviders.Add(keyId, cryptoServiceProvider);
            }

            cryptoServiceProviders = newCryptoServiceProviders;
        }

        static byte[] GetBytesFromPEM(string pem, string type = "CERTIFICATE")
        {
            string header = String.Format("-----BEGIN {0}-----", type);
            string footer = String.Format("-----END {0}-----", type);

            int start = pem.IndexOf(header) + header.Length;
            int end = pem.IndexOf(footer, start);

            string base64 = pem.Substring(start, (end - start));

            return Convert.FromBase64String(base64);
        }

        static double ToUnixTime(DateTime date)
        {
            return (date.ToUniversalTime() - epoch).TotalSeconds;
        }
    }

    #pragma warning disable IDE1006 // Naming Styles

    class Identities
    {
        public List<string> email { get; set; }
    }

    class FirebaseContent
    {
        public Identities identities { get; set; }
        public string sign_in_provider { get; set; }
    }

    class TokenPayload
    {
        public string iss { get; set; }
        public string aud { get; set; }
        public int auth_time { get; set; }
        public string user_id { get; set; }
        public string sub { get; set; }
        public int iat { get; set; }
        public int exp { get; set; }
        public string email { get; set; }
        public bool email_verified { get; set; }
        public FirebaseContent firebase { get; set; }
    }

    #pragma warning restore IDE1006 // Naming Styles
}
