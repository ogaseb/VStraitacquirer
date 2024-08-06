using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common.Entities;
using System.Linq;
using System;

namespace traitacquirer
{
    public class traitacquirerModSystem : ModSystem
    {
        // Called on server and client
        // Useful for registering block/entity classes on both sides
        ICoreAPI api;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        public List<Trait> traits = new List<Trait>();
        public List<CharacterClass> characterClasses = new List<CharacterClass>();
        public Dictionary<string, Trait> TraitsByCode = new Dictionary<string, Trait>();
        public Dictionary<string, CharacterClass> characterClassesByCode = new Dictionary<string, CharacterClass>();
        //public CharacterSystem characterSystem = new();

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.RegisterItemClass(Mod.Info.ModID + ".ItemTraitManual", typeof(ItemTraitManual));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            //traits = api.Assets.Get("config/traits.json").ToObject<List<Trait>>();
            loadCharacterClasses();
            api.Event.RegisterEventBusListener(OnAcquireTrait, 0.5, "traitAcquisition");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
        }

        private void OnAcquireTrait(string eventName, ref EnumHandling handling, IAttribute data)
        {
            TreeAttribute tree = data as TreeAttribute;
            string playerUid = tree.GetString("playeruid");
            string traitName = tree.GetString("trait");

            IServerPlayer plr = sapi.World.PlayerByUid(playerUid) as IServerPlayer;

            ItemSlot itemslot = plr.InventoryManager.ActiveHotbarSlot;
            Trait trait = traits.Find(x => x.Code == traitName);

            /*
            List<string> newExtraTraits = new List<string>();
            string[] extraTraits = byEntity.WatchedAttributes.GetStringArray("extraTraits");
            if (extraTraits != null) { 
                newExtraTraits.AddRange(extraTraits);
            }
            newExtraTraits.Add(tree.GetString("trait"));
            byEntity.WatchedAttributes.SetStringArray("extraTraits", newExtraTraits.ToArray());
            */
            

            if (trait != null)
            {
                //characterSystem.applyTraitAttributes(plr);
                List<string> newExtraTraits = new List<string>();
                string[] extraTraits = plr.Entity.WatchedAttributes.GetStringArray("extraTraits");
                if (extraTraits != null)
                {
                    newExtraTraits.AddRange(extraTraits);
                }
                newExtraTraits.Add(traitName);
                plr.Entity.WatchedAttributes.SetStringArray("extraTraits", newExtraTraits.ToArray());
                applyTraitAttributes(plr.Entity);
                plr.SendIngameError("Trait Acquire Attempt", Lang.Get("Trait Acquire Attempt"));
                //var chapters = (itemslot.Itemstack.Attributes["chapterIds"] as IntArrayAttribute).value;
                //discovery = new LoreDiscovery()
                //{
                //    Code = discCode,
                //    ChapterIds = new List<int>(chapters)
                //};
            }

            if (trait == null)
            {
                plr.SendIngameError("Trait is Null", Lang.Get("Trait is Null"));
                //plr.SendIngameError("alreadydiscovered", Lang.Get("Nothing new in these pages"));
                return;
            }

            itemslot.MarkDirty();
            plr.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/writing"), plr.Entity);

            handling = EnumHandling.PreventDefault;

            //DiscoverLore(discovery, plr, itemslot);
        }

        public void applyTraitAttributes(EntityPlayer eplr)
        {
            string classcode = eplr.WatchedAttributes.GetString("characterClass");
            CharacterClass charclass = characterClasses.FirstOrDefault(c => c.Code == classcode);
            if (charclass == null) throw new ArgumentException("Not a valid character class code!");

            // Reset 
            foreach (var stats in eplr.Stats)
            {
                foreach (var statmod in stats.Value.ValuesByKey)
                {
                    if (statmod.Key == "trait")
                    {
                        stats.Value.Remove(statmod.Key);
                        break;
                    }
                }
            }



            // Then apply
            //string[] addedTrait = []; 
            string[] extraTraits = eplr.WatchedAttributes.GetStringArray("extraTraits");
            var allTraits = extraTraits == null ? charclass.Traits : charclass.Traits.Concat(extraTraits);

            foreach (var traitcode in allTraits)
            {
                Trait trait;
                if (TraitsByCode.TryGetValue(traitcode, out trait))
                {
                    foreach (var val in trait.Attributes)
                    {
                        string attrcode = val.Key;
                        double attrvalue = val.Value;

                        eplr.Stats.Set(attrcode, "trait", (float)attrvalue, true);
                    }
                }
            }

            eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }
        private void loadCharacterClasses()
        {
            //onLoadedUniversal();
            traits = api.Assets.Get("config/traits.json").ToObject<List<Trait>>();
            characterClasses = api.Assets.Get("config/characterclasses.json").ToObject<List<CharacterClass>>();

            foreach (var trait in traits)
            {
                TraitsByCode[trait.Code] = trait;

                /*string col = "#ff8484";
                if (trait.Type == EnumTraitType.Positive) col = "#84ff84";
                if (trait.Type == EnumTraitType.Mixed) col = "#fff584";

                Console.WriteLine("\"trait-" + trait.Code + "\": \"<font color=\\"" + col + "\\">• " + trait.Code + "</font> ({0})\",");*/

                /*foreach (var val in trait.Attributes)
                {
                    Console.WriteLine("\"charattribute-" + val.Key + "-"+val.Value+"\": \"\",");
                }*/
            }

            foreach (var charclass in characterClasses)
            {
                characterClassesByCode[charclass.Code] = charclass;

                foreach (var jstack in charclass.Gear)
                {
                    if (!jstack.Resolve(api.World, "character class gear", false))
                    {
                        api.World.Logger.Warning("Unable to resolve character class gear " + jstack.Type + " with code " + jstack.Code + " item/bloc does not seem to exist. Will ignore.");
                    }
                }
            }
        }
    }
}
