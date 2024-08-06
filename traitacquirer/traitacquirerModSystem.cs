﻿using System.Collections.Generic;
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
using System.Data;
using System.Xml.Linq;
using System.Text;

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
        GuiDialogCharacterBase charDlg;

        GuiElementRichtext logtextElem;
        ElementBounds clippingBounds;// = ElementBounds.Fixed(0, 0, innerWidth - 20 - 1, 200 - 1).FixedUnder(commmandsBounds, spacing - 10);
        ElementBounds scrollbarBounds;// = clippingBounds.CopyOffsetedSibling(clippingBounds.fixedWidth + 6, -1).WithFixedWidth(20).FixedGrow(0, 2);
        ElementBounds commmandsBounds;// = ElementBounds.Fixed(0, 30, innerWidth, 30);
        int spacing = 5;
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.RegisterItemClass(Mod.Info.ModID + ".ItemTraitManual", typeof(ItemTraitManual));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            loadCharacterClasses();
            api.Event.RegisterEventBusListener(OnAcquireTrait, 0.5, "traitAcquisition");
            //api.RegisterCommand();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            loadCharacterClasses();
            charDlg = api.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            charDlg.RenderTabHandlers.Add(composeTraitsTab);
        }

        private void composeTraitsTab(GuiComposer compo)
        {
            charDlg.SingleComposer.GetTextArea("");
            this.clippingBounds = ElementBounds.Fixed(0, 25, 385 - 5 - 1, 200 - 1);//.FixedUnder(commmandsBounds, spacing - 10);
            compo.BeginClip(clippingBounds);
            compo.AddRichtext(getClassTraitText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), ElementBounds.Fixed(0, 25, 385, 200), "text");
            //compo.AddDynamicText(getClassTraitText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), ElementBounds.Fixed(0, 25, 385, 200), "text");
            this.logtextElem = compo.GetRichtext("text");
            //logtextElem.Bounds.CalcWorldBounds();
            //double innerWidth = logtextElem.Bounds.absInnerWidth;
            //this.commmandsBounds = ElementBounds.Fixed(0, 30, innerWidth, 30);
            this.scrollbarBounds = clippingBounds.CopyOffsetedSibling(clippingBounds.fixedWidth + 6, -1).WithFixedWidth(20).FixedGrow(0, 2);

            compo.AddVerticalScrollbar(OnNewScrollbarvalue, this.scrollbarBounds, "scrollbar");
            compo.EndClip();
        }
        private void OnNewScrollbarvalue(float value)
        {
            //GuiElementDynamicText logtextElem = compo.GetDynamicText("text");
            logtextElem.Bounds.fixedY = 3 - value;
            logtextElem.Bounds.CalcWorldBounds();
        }

        string getClassTraitText()
        {
            string charClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
            CharacterClass chclass = characterClasses.FirstOrDefault(c => c.Code == charClass);

            StringBuilder fulldesc = new StringBuilder();
            StringBuilder attributes = new StringBuilder();

            var chartraits = chclass.Traits.Select(code => TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

            foreach (var trait in chartraits)
            {
                attributes.Clear();
                foreach (var val in trait.Attributes)
                {
                    if (attributes.Length > 0) attributes.Append(", ");
                    attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", val.Key, val.Value)));
                }

                if (attributes.Length > 0)
                {
                    fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
                }
                else
                {
                    string desc = Lang.GetIfExists("traitdesc-" + trait.Code);
                    if (desc != null)
                    {
                        fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), desc));
                    }
                    else
                    {
                        fulldesc.AppendLine(Lang.Get("trait-" + trait.Code));
                    }


                }
            }

            if (chclass.Traits.Length == 0)
            {
                fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
            }

            fulldesc.AppendLine(Lang.Get("Extra Traits: "));

            string[] extraTraits = capi.World.Player.Entity.WatchedAttributes.GetStringArray("extraTraits");

            var extratraits = extraTraits.OrderBy(code => (int)TraitsByCode[code].Type);
            //var extratraits = chclass.Traits.Select(code => TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

            foreach (var code in extratraits)
            {
                attributes.Clear();
                foreach (var val in TraitsByCode[code].Attributes)
                {
                    if (attributes.Length > 0) attributes.Append(", ");
                    attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", val.Key, val.Value)));
                }

                if (attributes.Length > 0)
                {
                    fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + code), attributes));
                }
                else
                {
                    string desc = Lang.GetIfExists("traitdesc-" + code);
                    if (desc != null)
                    {
                        fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + code), desc));
                    }
                    else
                    {
                        fulldesc.AppendLine(Lang.Get("trait-" + code));
                    }


                }
            }

            return fulldesc.ToString();
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
                if (!newExtraTraits.Contains(traitName))
                {
                    newExtraTraits.Add(traitName);
                    plr.Entity.WatchedAttributes.SetStringArray("extraTraits", newExtraTraits.ToArray());
                    applyTraitAttributes(plr.Entity);
                    plr.SendIngameError("Trait Acquired", Lang.Get("Trait Acquired"));
                }
                else
                {
                    plr.SendIngameError("Trait Already Acquired", Lang.Get("Trait Already Acquired"));
                }
                
            }

            if (trait == null)
            {
                plr.SendIngameError("Trait is Null", Lang.Get("Trait is Null"));
                return;
            }

            itemslot.MarkDirty();
            plr.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/writing"), plr.Entity);

            handling = EnumHandling.PreventDefault;

            //DiscoverLore(discovery, plr, itemslot);
        }

        public void applyTraitAttributes(EntityPlayer eplr) //Taken from SurvivalMod Character.cs, CharacterSystem class where it is a private method
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
        private void loadCharacterClasses() //Taken from SurvivalMod Character.cs, CharacterSystem class where it is a private method
        {
            //onLoadedUniversal();
            this.traits = api.Assets.Get("config/traits.json").ToObject<List<Trait>>();
            this.characterClasses = api.Assets.Get("config/characterclasses.json").ToObject<List<CharacterClass>>();

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
