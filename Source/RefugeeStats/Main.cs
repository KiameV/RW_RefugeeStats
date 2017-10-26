using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

namespace RefugeeStats
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("com.refugeestats.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("RefugeeStats: Adding Harmony Prefix to IncidentWorker_RefugeeChased.TryExecuteWorker (blocking).");
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RefugeeChased), "TryExecuteWorker")]
    public static class Patch_IncidentWorker_RefugeeChased_TryExecuteWorker
    {
        private const int TITLE_SIZE = 18;

        static bool Prefix(ref bool __result, ref IncidentWorker_RefugeeChased __instance, IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IntVec3 spawnSpot;
            Predicate<IntVec3> pointExists = (IntVec3 c) => map.reachability.CanReachColony(c);
            if (!CellFinder.TryFindRandomEdgeCellWith(
                pointExists, map, CellFinder.EdgeRoadChance_Neutral, out spawnSpot))
            {
                __result = false;
                return false;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.SpaceRefugee, Faction.OfSpacer, PawnGenerationContext.NonPlayer, -1, false, false, false, false, true, false, 20f, false, true, true, false, false, false, false, null, null, null, null, null, null, null);
            Pawn refugee = PawnGenerator.GeneratePawn(request);
            refugee.relations.everSeenByPlayer = true;
            Faction enemyFac;
            if (!(from f in Find.FactionManager.AllFactions
                  where !f.def.hidden && f.HostileTo(Faction.OfPlayer)
                  select f).TryRandomElement(out enemyFac))
            {
                __result = false;
                return false;
            }

            string text = CreateRefugeeText(refugee, enemyFac);

            CreateDialogOptions(text, refugee, spawnSpot, map, enemyFac);

            __result = true;
            return false;
        }

        #region Helper_Methods

        private static string CreateRefugeeText(Pawn refugee, Faction enemyFac)
        {
            string text = "RefugeeChasedInitial".Translate(new object[]
            {
                refugee.Name.ToStringFull,
                refugee.story.Title.ToLower(),
                enemyFac.def.pawnsPlural,
                enemyFac.Name,
                refugee.ageTracker.AgeBiologicalYears,
            });
            StringBuilder sb = new StringBuilder(text.AdjustedFor(refugee));

            String s = "";
            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref s, refugee);
            sb.Append(s);

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine();

            AppendStats(sb, refugee);

            return sb.ToString();
        }

        private static void AppendStats(StringBuilder sb, Pawn refugee)
        {
            AppendDisabledWorkTags(sb, refugee.story.CombinedDisabledWorkTags);

            sb.AppendLine();
            sb.AppendLine();

            AppendTraits(sb, refugee.story.traits.allTraits);

            sb.AppendLine();
            sb.AppendLine();

            AppendPassions(sb, refugee.skills.skills);
        }

        private static void AppendDisabledWorkTags(StringBuilder sb, WorkTags disabledWorkTags)
        {
            sb.Append(CreateTitle("IncapableOf".Translate()));
            sb.AppendLine();

            if (disabledWorkTags == 0)
            {
                sb.Append("(" + "NoneLower".Translate() + ")");
            }
            else
            {
                int count = 0;
                bool capitalize = true;
                foreach (WorkTags workTag in Enum.GetValues(typeof(WorkTags)))
                {
                    if (workTag != 0 && (disabledWorkTags & workTag) == workTag)
                    {
                        if (count > 0)
                        {
                            sb.Append(", ");
                        }
                        if (!capitalize)
                        {
                            sb.Append(workTag.LabelTranslated().ToLower());
                        }
                        else
                        {
                            sb.Append(workTag.LabelTranslated());
                        }
                        ++count;
                        capitalize = false;
                    }
                }
            }
        }

        private static void AppendTraits(StringBuilder sb, List<Trait> traits)
        {
            sb.Append(CreateTitle("Traits".Translate()));

            // Here we add the new line before the label so there isn't an extra one after it.
            foreach (Trait trait in traits)
            {
                sb.AppendLine();
                sb.Append(trait.LabelCap);
            }
        }

        private static void AppendPassions(StringBuilder sb, List<SkillRecord> skills)
        {
            sb.Append(CreateTitle("Passionate for"));
            sb.AppendLine();

            if (skills.Count == 0)
            {
                sb.Append("(" + "NoneLower".Translate() + ")");
            }
            else
            {
                int count = 0;
                foreach (SkillRecord skill in skills)
                {
                    if (skill.passion > Passion.None)
                    {
                        if (count > 0)
                        {
                            sb.Append(", ");
                        }
                        sb.Append(skill.def.skillLabel + (skill.passion == Passion.Major ? " (Major)" : ""));
                        ++count;
                    }
                }
            }
        }

        private static string CreateTitle(string title)
        {
            return "<size=" + TITLE_SIZE + ">" + title + "</size>";
        }

        private static void CreateDialogOptions(string text, Pawn refugee, IntVec3 spawnSpot, Map map, Faction enemyFac)
        {
            DiaNode diaNode = new DiaNode(text);
            DiaOption diaOption = CreateAcceptOption(refugee, spawnSpot, map, enemyFac);
            diaNode.options.Add(diaOption);

            string text2 = "RefugeeChasedRejected".Translate(new object[]
            {
                refugee.NameStringShort
            });
            DiaNode diaNode2 = new DiaNode(text2);
            DiaOption diaOption2 = new DiaOption("OK".Translate());
            diaOption2.resolveTree = true;
            diaNode2.options.Add(diaOption2);
            DiaOption diaOption3 = new DiaOption("RefugeeChasedInitial_Reject".Translate());
            diaOption3.action = delegate
            {
                Find.WorldPawns.PassToWorld(refugee, PawnDiscardDecideMode.Decide);
            };
            diaOption3.link = diaNode2;
            diaNode.options.Add(diaOption3);
            string title = "RefugeeChasedTitle".Translate(new object[]
            {
                map.info.parent.Label
            });
            Find.WindowStack.Add(new Dialog_NodeTree(diaNode, true, true, title));
        }

        private static DiaOption CreateAcceptOption(Pawn refugee, IntVec3 spawnSpot, Map map, Faction enemyFac)
        {
            DiaOption diaOption = new DiaOption("RefugeeChasedInitial_Accept".Translate());
            diaOption.action = delegate
            {
                GenSpawn.Spawn(refugee, spawnSpot, map);
                refugee.SetFaction(Faction.OfPlayer, null);
                Find.CameraDriver.JumpToVisibleMapLoc(spawnSpot);
                IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(Find.Storyteller.def, IncidentCategory.ThreatBig, map);
                incidentParms.forced = true;
                incidentParms.faction = enemyFac;
                incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                incidentParms.raidArrivalMode = PawnsArriveMode.EdgeWalkIn;
                incidentParms.spawnCenter = spawnSpot;
                incidentParms.points *= 1.35f;

                IntRange raidDelay = (IntRange)typeof(IncidentWorker_RefugeeChased).GetField("RaidDelay", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                QueuedIncident qi = new QueuedIncident(new FiringIncident(IncidentDefOf.RaidEnemy, null, incidentParms), Find.TickManager.TicksGame + raidDelay.RandomInRange);
                Find.Storyteller.incidentQueue.Add(qi);
            };
            diaOption.resolveTree = true;
            return diaOption;
        }
        #endregion
    }
}
