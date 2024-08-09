using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.GameContent;

namespace traitacquirer
{
    internal class traitacquirerConfig
    {
        public static traitacquirerConfig Current { get; set; }

        public string acquireCmdPrivilege = "gamemode";
        public string giveCmdPrivilege = "root";
        public string classManuals = "true";

        public static traitacquirerConfig GetDefault()
        {
            traitacquirerConfig config =  new traitacquirerConfig();
            config.acquireCmdPrivilege.ToString();
            config.giveCmdPrivilege.ToString();
            config.classManuals.ToString();
            return config;
        }
    }
}
