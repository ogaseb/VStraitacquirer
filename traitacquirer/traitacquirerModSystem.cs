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
using System.Data;
using System.Xml.Linq;
using System.Text;
using Vintagestory.API.MathTools;
using System.Diagnostics;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;
using Vintagestory.ServerMods.NoObf;

namespace traitacquirer
{
    public class traitacquirerModSystem : ModSystem
    {
        ModJsonPatchLoader patch;
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

        GuiElementRichtext richtextElem;
        ElementBounds clippingBounds;
        ElementBounds scrollbarBounds;
        int spacing = 5;
        public override void Start(ICoreAPI api)
        {
            this.api = api;

            //Register Classes
            api.RegisterItemClass(Mod.Info.ModID + ".ItemTraitManual", typeof(ItemTraitManual));
            //Load Config
            try
            {
                traitacquirerConfig traitacquirerConfig = api.LoadModConfig<traitacquirerConfig>("traitacquirer.json");
                if (traitacquirerConfig != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                    traitacquirerConfig.Current = traitacquirerConfig;
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    traitacquirerConfig.Current = traitacquirerConfig.GetDefault();
                }
            }
            catch
            {
                traitacquirerConfig.Current = traitacquirerConfig.GetDefault();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig<traitacquirerConfig>(traitacquirerConfig.Current, "traitacquirer.json");
            }
            //Set World Config
            api.World.Config.SetBool("classManuals", traitacquirerConfig.Current.classManuals);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            loadCharacterClasses();
            api.Event.RegisterEventBusListener(AcquireTraitEventHandler, 0.5, "traitItem");
            acquireTraitCommand();
            giveTraitCommand();
        }

        public void acquireTraitCommand()
        {
            var parsers = sapi.ChatCommands.Parsers;
            sapi.ChatCommands.Create("acquireTrait")
            .WithDescription("Gives the caller the given Trait, removes with the rm flag")
            .RequiresPrivilege(traitacquirerConfig.Current.acquireCmdPrivilege)
            .RequiresPlayer()
            .WithArgs(parsers.Word("trait name"), parsers.OptionalWordRange("remove flag", "rm"))
            .HandleWith((args) =>
            {
                var byEntity = args.Caller.Entity;
                string exitMessage;
                string traitName = args[0].ToString();
                if (traits.Find(x => x.Code == traitName) == null)
                {
                    return TextCommandResult.Error("Trait does not exist");
                }
                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                if ((string)args[1] == "rm")
                {
                    processTraits(byPlayer?.PlayerUID, new string[0], new string[] { traitName });
                    exitMessage = "Trait Removed";
                }
                else
                {
                    processTraits(byPlayer?.PlayerUID, new string[] { traitName }, new string[0]);
                    exitMessage = "Trait given";
                }
                return TextCommandResult.Success(exitMessage);
            });
        }

        public void giveTraitCommand()
        {
            var parsers = sapi.ChatCommands.Parsers;
            sapi.ChatCommands.Create("giveTrait")
            .WithDescription("Gives the given Trait to the chosen player, removes with the rm flag")
            .RequiresPrivilege(traitacquirerConfig.Current.giveCmdPrivilege)
            .RequiresPlayer()
            .WithArgs(parsers.Word("trait name"), parsers.OnlinePlayer("target player"), parsers.OptionalWordRange("remove flag", "rm"))
            .HandleWith((args) =>
            {
                IServerPlayer targetPlayer = (IServerPlayer)args[1];
                var byEntity = args.Caller.Entity;
                string exitMessage;
                string traitName = args[0].ToString();
                if (traits.Find(x => x.Code == traitName) == null)
                {
                    return TextCommandResult.Error("Trait does not exist");
                }
                if ((string)args[1] == "rm")
                {
                    processTraits(targetPlayer?.PlayerUID, new string[0], new string[] { traitName });
                    exitMessage = "Trait Removed";
                }
                else
                {
                    processTraits(targetPlayer?.PlayerUID, new string[] { traitName }, new string[0]);
                    exitMessage = "Trait given";
                }
                return TextCommandResult.Success(exitMessage);
            });
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

            this.clippingBounds = ElementBounds.Fixed(0, 25, 385, 310);
            compo.BeginClip(clippingBounds);
            compo.AddRichtext(getClassTraitText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), ElementBounds.Fixed(0, 0, 385, 310), "text");
            compo.EndClip();
            this.scrollbarBounds = clippingBounds.CopyOffsetedSibling(clippingBounds.fixedWidth - 3, -6).WithFixedWidth(6).FixedGrow(0, 2);
            compo.AddVerticalScrollbar(OnNewScrollbarValue, this.scrollbarBounds, "scrollbar");
            this.richtextElem = compo.GetRichtext("text");

            compo.GetScrollbar("scrollbar").SetHeights(
                (float)100, (float)310
            );
        }
        private void OnNewScrollbarValue(float value)
        {
            richtextElem.Bounds.fixedY = 10 - value;
            richtextElem.Bounds.CalcWorldBounds();

        }

        string getClassTraitText()
        {
            string charClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
            CharacterClass chclass = characterClasses.FirstOrDefault(c => c.Code == charClass);

            StringBuilder fulldesc = new StringBuilder();
            StringBuilder attributes = new StringBuilder();

            fulldesc.AppendLine(Lang.Get("Class Traits: "));

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
            IOrderedEnumerable<string> extratraits = Enumerable.Empty<string>().OrderBy(x => 1); ;
            if (extraTraits != null)
            {
                extratraits = extraTraits?.OrderBy(code => (int)TraitsByCode[code].Type);
            }

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

        public void AcquireTraitEventHandler(string eventName, ref EnumHandling handling, IAttribute data)
        {
            TreeAttribute tree = data as TreeAttribute;
            string playerUid = tree.GetString("playeruid");
            string[] addtraits = tree.GetStringArray("addtraits");
            string[] removetraits = tree.GetStringArray("removetraits");
            processTraits(playerUid, addtraits, removetraits);
        }

        public void processTraits(string playerUid, string[] addtraits, string[] removetraits)
        {
            IServerPlayer plr = sapi.World.PlayerByUid(playerUid) as IServerPlayer;
            List<string> newExtraTraits = new List<string>();
            string[] extraTraits = plr.Entity.WatchedAttributes.GetStringArray("extraTraits");
            if (extraTraits != null)
            {
                newExtraTraits.AddRange(extraTraits);
            }
            foreach (string traitName in addtraits) 
            {
                Trait trait = traits.Find(x => x.Code == traitName);
                if (trait == null)
                {
                    plr.SendIngameError("Trait is Null", Lang.Get("Trait is Null"));
                    return;
                }
                if (!newExtraTraits.Contains(traitName))
                {
                    newExtraTraits.Add(traitName);
                }
                else
                {
                    plr.SendIngameError("Trait Already Acquired", Lang.Get("Trait Already Acquired"));
                }
            }
            foreach (string traitName in removetraits)
            {
                Trait trait = traits.Find(x => x.Code == traitName);
                if (trait == null)
                {
                    plr.SendIngameError("Trait is Null", Lang.Get("Trait is Null"));
                    return;
                }
                if (newExtraTraits.Contains(traitName))
                {
                    newExtraTraits.Remove(traitName);
                }
            }
            plr.Entity.WatchedAttributes.SetStringArray("extraTraits", newExtraTraits.ToArray());
            applyTraitAttributes(plr.Entity);
            plr.Entity.WatchedAttributes.MarkPathDirty("extraTraits");
            plr.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/writing"), plr.Entity);
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
        public void loadCharacterClasses() //Taken from SurvivalMod Character.cs, CharacterSystem class where it is a private method
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
