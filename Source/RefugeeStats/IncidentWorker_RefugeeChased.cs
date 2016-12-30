using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RefugeeStats
{
    public class IncidentWorker_RefugeeChased : IncidentWorker
    {
        private const float RaidPointsFactor = 1.35f;

        private const float RelationWithColonistWeight = 20f;

        private static readonly IntRange RaidDelay = new IntRange(1000, 2500);

        private int titleSize = 18;

        public override bool TryExecute(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IntVec3 spawnSpot;
            if (!CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => map.reachability.CanReachColony(c), map, out spawnSpot))
            {
                return false;
            }

            Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Spacer);
            PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.SpaceRefugee, faction, PawnGenerationContext.NonPlayer, null, false, false, false, false, true, false, 20f, false, true, true, null, null, null, null, null, null);
            Pawn refugee = PawnGenerator.GeneratePawn(request);
            refugee.relations.everSeenByPlayer = true;
            Faction enemyFac;
            if (!(from f in Find.FactionManager.AllFactions
                  where !f.def.hidden && f.HostileTo(Faction.OfPlayer)
                  select f).TryRandomElement(out enemyFac))
            {
                return false;
            }

            string text = CreateRefugeeText(refugee, enemyFac);

            CreateDialogOptions(text, refugee, spawnSpot, map, enemyFac);

            return true;
        }

        private string CreateRefugeeText(Pawn refugee, Faction enemyFac)
        {
            string text = "RefugeeChasedInitial".Translate(new object[]
            {
                refugee.Name.ToStringFull,
                refugee.story.Title.ToLower(),
                enemyFac.def.pawnsPlural,
                enemyFac.Name,
                refugee.ageTracker.AgeBiologicalYears,
            });
            text = text.AdjustedFor(refugee);

            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, refugee);

            text += Environment.NewLine;
            text += Environment.NewLine;
            text += Environment.NewLine;

            AppendStats(ref text, refugee);

            return text;
        }

        private void AppendStats(ref string text, Pawn refugee)
        {
            AppendDisabledWorkTags(ref text, refugee.story.DisabledWorkTags.ToList<WorkTags>());

            text += Environment.NewLine;
            text += Environment.NewLine;

            AppendTraits(ref text, refugee.story.traits.allTraits);

            text += Environment.NewLine;
            text += Environment.NewLine;

            AppendPassions(ref text, refugee.skills.skills);
        }

        private void AppendDisabledWorkTags(ref string text, List<WorkTags> disabledWorkTags)
        {
            text += CreateTitle("IncapableOf".Translate());
            text += Environment.NewLine;

            if (disabledWorkTags.Count == 0)
            {
                text += "(" + "NoneLower".Translate() + ")";
            }
            else
            {
                bool capitalize = true;
                foreach (WorkTags workTag in disabledWorkTags)
                {
                    if (!capitalize)
                    {
                        text += workTag.LabelTranslated().ToLower();
                    }
                    else
                    {
                        text += workTag.LabelTranslated();
                    }
                    text += ", ";
                    capitalize = false;
                }

                text = text.Substring(0, text.Length - 2);
            }
        }

        private void AppendTraits(ref string text, List<Trait> traits)
        {
            text += CreateTitle("Traits".Translate());

            // Here we add the new line before the label so there isn't an extra one after it.
            foreach (Trait trait in traits)
            {
                text += Environment.NewLine;
                text += trait.LabelCap;
            }
        }

        private void AppendPassions(ref string text, List<SkillRecord> skills)
        {
            text += CreateTitle("Passionate for");
            text += Environment.NewLine;

            if (skills.Count == 0)
            {
                text += "(" + "NoneLower".Translate() + ")";
            }
            else
            {
                foreach (SkillRecord skill in skills)
                {
                    if (skill.passion > Passion.None)
                    {
                        text += skill.def.skillLabel + (skill.passion == Passion.Major ? " (Major)" : "");
                        text += ", ";
                    }
                }

                text = text.Substring(0, text.Length - 2);
            }
        }

        private string CreateTitle(string title)
        {
            return "<size=" + titleSize + ">" + title + "</size>";
        }

        private void CreateDialogOptions(string text, Pawn refugee, IntVec3 spawnSpot, Map map, Faction enemyFac)
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
            Find.WindowStack.Add(new Dialog_NodeTree(diaNode, true, true));
        }

        private DiaOption CreateAcceptOption(Pawn refugee, IntVec3 spawnSpot, Map map, Faction enemyFac)
        {
            DiaOption diaOption = new DiaOption("RefugeeChasedInitial_Accept".Translate());
            diaOption.action = delegate
            {
                GenSpawn.Spawn(refugee, spawnSpot, map);
                refugee.SetFaction(Faction.OfPlayer, null);
                Find.CameraDriver.JumpTo(spawnSpot);
                IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(Find.Storyteller.def, IncidentCategory.ThreatBig, map);
                incidentParms.forced = true;
                incidentParms.faction = enemyFac;
                incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                incidentParms.raidArrivalMode = PawnsArriveMode.EdgeWalkIn;
                incidentParms.spawnCenter = spawnSpot;
                incidentParms.points *= 1.35f;
                QueuedIncident qi = new QueuedIncident(new FiringIncident(IncidentDefOf.RaidEnemy, null, incidentParms), Find.TickManager.TicksGame + IncidentWorker_RefugeeChased.RaidDelay.RandomInRange);
                Find.Storyteller.incidentQueue.Add(qi);
            };
            diaOption.resolveTree = true;
            return diaOption;
        }
    }
}