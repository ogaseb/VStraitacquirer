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
        public string listCmdPrivilege = "chat";
        public bool classManuals = true;
        public double manualsAvgPrice = 10;
        //public double manualsVarPrice = 4;
        //public double manualsAvgStock = 1;
        //public double manualsVarStock = 0.25;
        //public double manualsAvgLoot = 1;
        //public double manualsVarLoot = 0.25;

        public static traitacquirerConfig GetDefault()
        {
            traitacquirerConfig config =  new traitacquirerConfig();
            config.acquireCmdPrivilege.ToString();
            config.giveCmdPrivilege.ToString();
            config.listCmdPrivilege.ToString();
            config.classManuals = true;
            //config.manualsAvgPrice = 10;
            //config.manualsVarPrice = 4;
            //config.manualsAvgStock = 1;
            //config.manualsVarStock = 0.25;
            //config.manualsAvgLoot = 1;
            //config.manualsVarLoot = 0.25;
            return config;
        }
    }
}
