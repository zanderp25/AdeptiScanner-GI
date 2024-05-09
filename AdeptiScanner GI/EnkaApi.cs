﻿using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System;
using System.Net.Http.Headers;

namespace AdeptiScanner_GI
{
    internal static class EnkaApi
    {

        static List<Character> ProcessData(JObject enkaJson)
        {
            List<Character> characters = new List<Character>();
            if (!enkaJson.TryGetValue("avatarInfoList", out JToken avatarInfo))
            {
                return null;
            }

            List<JObject> chars = avatarInfo.ToObject<List<JObject>>();
            foreach (JObject charJson in chars)
            {
                characters.Add(Character.FromEnkaData(charJson));
            }
            return characters;
        }

        public static TimeSpan GetRemainingCooldown()
        {
            TimeSpan remainingWait = cooldownEnd - DateTime.Now;
            if (remainingWait <= TimeSpan.Zero)
            {
                // if it's over, no point in specifying by how much. Better to just avoid negative numbers 
                return TimeSpan.Zero;
            }
            return remainingWait;
        }

        public static void RequestUid(string uid)
        {
            lock (EnkaRequestLock)
            {
                if (occupied)
                {
                    ScannerForm.INSTANCE.AppendStatusText("Enka request already in progress"  + Environment.NewLine, false);
                    return;
                }

                TimeSpan remainingWait = cooldownEnd - DateTime.Now;

                if (remainingWait.TotalSeconds > 0d)
                {
                    ScannerForm.INSTANCE.AppendStatusText("Enka request still on cooldown: " + remainingWait.TotalSeconds.ToString("F1") + "s" + Environment.NewLine, false);
                    return;
                }

                occupied = true;
            }


            string requestUrl = @"https://enka.network/api/uid/" + uid;

            var ans = client.GetAsync(requestUrl, HttpCompletionOption.ResponseContentRead);

            ans.Wait();

            HttpResponseMessage result = ans.Result;
            int waitTime = 5;

            if (result.IsSuccessStatusCode)
            {
                string content = result.Content.ReadAsStringAsync().Result;

                JObject enkaJson = JsonConvert.DeserializeObject<JObject>(content);

                if (enkaJson.TryGetValue("ttl", out JToken ttl))
                {
                    waitTime = ttl.ToObject<int>();
                }

                var chars = ProcessData(enkaJson);
                if (chars != null)
                {
                    ScannerForm.INSTANCE.UpdateCharacterList(chars);
                } else
                {
                    ScannerForm.INSTANCE.AppendStatusText(Environment.NewLine + "Enka data contained no character details." + Environment.NewLine 
                        + "Character showcase is either empty or set to \"Hide Character Details\"." + Environment.NewLine
                        + "Please add some characters to your showcase and read character import section of the instructions for usage." + Environment.NewLine + Environment.NewLine, false);
                }
                ScannerForm.INSTANCE.AppendStatusText("Enka request done, cooldown: " + waitTime + "s" + Environment.NewLine, false);
            }
            else
            {
                ScannerForm.INSTANCE.AppendStatusText("Enka request FAILED with status code: " + result.StatusCode + Environment.NewLine 
                    + "Make sure the UID you entered is valid, and verify Enka is up by trying in your browser" + Environment.NewLine, false);
            }

            lock (EnkaRequestLock)
            {

                cooldownEnd = DateTime.Now + TimeSpan.FromSeconds(waitTime);

                occupied = false;
            }
        }

        private static object EnkaRequestLock = "hi"; //this just has to be a reference type
        private static bool occupied = false;

        private static DateTime cooldownEnd = DateTime.MinValue;

        private static HttpClient client;

        static EnkaApi()
        {
            WebProxy proxy = null;
            string proxy_string = Environment.GetEnvironmentVariable("http_proxy");
            if (proxy_string != null)
            {
                proxy = new WebProxy(new Uri(proxy_string));
            }
            HttpClientHandler handler = new HttpClientHandler
            {
                Proxy = proxy,
                UseCookies = false
            };
            client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AdeptiScanner", Database.programVersion));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
