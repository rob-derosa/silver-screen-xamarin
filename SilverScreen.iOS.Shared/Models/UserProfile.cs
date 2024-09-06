using System;
using Newtonsoft.Json;

namespace SilverScreen.iOS.Shared
{
    public class UserProfile
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("private")]
        public bool Private { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("vip")]
        public bool Vip { get; set; }

        [JsonProperty("vip_ep")]
        public bool VipEp { get; set; }
    }

}

