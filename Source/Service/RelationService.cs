using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimTalk.Util;
using RimWorld;
using Verse;

namespace RimTalk.Service;

public static class RelationsService
{
    private const float FriendOpinionThreshold = 20f;
    private const float RivalOpinionThreshold = -20f;

    public static string GetRelationsString(Pawn pawn)
    {
        if (pawn?.relations == null) return "";

        StringBuilder relationsSb = new StringBuilder();
        HashSet<Pawn> relatedPawns = new HashSet<Pawn>();

        // 1. Get all pawns with explicit relations (global)
        if (pawn.relations.RelatedPawns != null)
        {
            foreach (var p in pawn.relations.RelatedPawns)
            {
                if (p != null) relatedPawns.Add(p);
            }
        }

        // 2. Add nearby pawns or pawns with non-zero opinion
        var nearby = PawnSelector.GetAllNearByPawns(pawn);
        if (nearby != null)
        {
            foreach (var p in nearby)
            {
                if (p != null) relatedPawns.Add(p);
            }
        }

        foreach (Pawn otherPawn in relatedPawns.OrderByDescending(p => Math.Abs(pawn.relations.OpinionOf(p))))
        {
            if (otherPawn == pawn || otherPawn.Dead ||
                otherPawn.relations is { hidePawnRelations: true }) continue;
            
            if (!otherPawn.RaceProps.Humanlike && !otherPawn.HasVocalLink()) continue;

            string label = null;

            try
            {
                float opinionValue = pawn.relations.OpinionOf(otherPawn);

                // --- Step 1: Check for the most important direct or family relationship ---
                PawnRelationDef mostImportantRelation = pawn.GetMostImportantRelation(otherPawn);
                if (mostImportantRelation != null)
                {
                    label = mostImportantRelation.GetGenderSpecificLabelCap(otherPawn);
                }

                // --- Step 2: If no family relation, check for an overriding status (master, slave, etc.) ---
                if (string.IsNullOrEmpty(label))
                {
                    label = GetStatusLabel(pawn, otherPawn);
                }

                // --- Step 3: Fallback to opinion-based relationship ---
                if (string.IsNullOrEmpty(label) && !pawn.IsVisitor() && !pawn.IsEnemy())
                {
                    if (opinionValue > 0)
                    {
                        label = "Friend".Translate();
                    }
                    else if (opinionValue < 0)
                    {
                        label = "Rival".Translate();
                    }
                    else
                    {
                        label = "Acquaintance".Translate();
                    }
                }

                // If we found any relevant relationship, add it to the string.
                if (!string.IsNullOrEmpty(label))
                {
                    string pawnName = otherPawn.LabelShort;
                    string brief = GetPawnBrief(otherPawn);
                    string opinion = opinionValue.ToStringWithSign();
                    relationsSb.Append($"{pawnName}({label}){brief} {opinion}, ");
                }
                
                // Prevent prompt overflow
                if (relationsSb.Length > 1000) break;
            }
            catch (Exception)
            {
                // Skip this pawn if opinion calculation fails
            }
        }

        if (relationsSb.Length > 0)
        {
            // Remove the trailing comma and space
            relationsSb.Length -= 2;
            return "Relations: " + relationsSb;
        }

        return "";
    }

    private static string GetPawnBrief(Pawn p)
    {
        string ageStr;
        if (p.ageTracker.AgeBiologicalYears > 0)
        {
            ageStr = $"{p.ageTracker.AgeBiologicalYears}-year-old";
        }
        else
        {
            // Handle babies under 1 year old
            long babyDays = p.ageTracker.AgeBiologicalTicks / 60000;
            ageStr = babyDays <= 0 ? "newborn" : $"{babyDays}-day-old";
        }

        string ageGender = $"{ageStr} {p.gender.ToString().ToLower()}";
        string job = p.story?.Adulthood?.TitleCapFor(p.gender).ToString() ?? p.story?.Childhood?.TitleCapFor(p.gender).ToString() ?? "Unknown";

        string status;
        if (p.IsColonist) status = "Colonist";
        else if (p.IsPrisoner) status = "Prisoner";
        else if (p.IsSlave) status = "Slave";
        else if (p.Faction != null && p.Faction.IsPlayer) status = "Member";
        else status = p.Faction?.Name ?? "Neutral";

        return $"[{ageGender}, {job}, {status}]";
    }

    public static string GetAllSocialString(Pawn pawn)
    {
        if (pawn?.relations == null) return "";

        var others = new HashSet<Pawn>();
        if (pawn.Map?.mapPawns?.AllPawnsSpawned != null)
        {
            foreach (var p in pawn.Map.mapPawns.AllPawnsSpawned)
                if (p != null) others.Add(p);
        }

        if (Find.WorldPawns != null)
        {
            foreach (var p in Find.WorldPawns.AllPawnsAlive)
                if (p != null) others.Add(p);
        }

        foreach (var p in pawn.relations.RelatedPawns)
        {
            if (p != null) others.Add(p);
        }

        others.Remove(pawn);

        var relationsSb = new StringBuilder();
        foreach (var otherPawn in others.OrderBy(p => p.LabelShort))
        {
            if ((!otherPawn.RaceProps.Humanlike && !otherPawn.HasVocalLink()) || otherPawn.Dead ||
                otherPawn.relations is { hidePawnRelations: true }) continue;

            if (TryGetSocialLabel(pawn, otherPawn, out var label, out var opinionValue))
            {
                string pawnName = otherPawn.LabelShort;
                string opinion = opinionValue.ToStringWithSign();
                relationsSb.Append($"{pawnName}({label}) {opinion}, ");
            }
        }

        if (relationsSb.Length > 0)
        {
            relationsSb.Length -= 2;
            return "Social: " + relationsSb;
        }

        return "";
    }

    public static string GetAllRelationsString(Pawn pawn)
    {
        if (pawn?.relations == null) return "";

        var related = pawn.relations.RelatedPawns?.ToList();
        if (related.NullOrEmpty()) return "";

        var sb = new StringBuilder();
        foreach (var otherPawn in related.Where(p => p != null && p != pawn).OrderBy(p => p.LabelShort))
        {
            var relationLabels = pawn.GetRelations(otherPawn)
                .Select(r => r?.GetGenderSpecificLabelCap(otherPawn).ToString())
                .Where(label => !string.IsNullOrEmpty(label))
                .ToList();

            if (relationLabels.Count == 0) continue;

            sb.Append($"{otherPawn.LabelShort}({string.Join("/", relationLabels)}), ");
        }

        if (sb.Length > 0)
        {
            sb.Length -= 2;
            return "Relations: " + sb;
        }

        return "";
    }

    private static string GetStatusLabel(Pawn pawn, Pawn otherPawn)

    {
        // Master relationship
        if ((pawn.IsPrisoner || pawn.IsSlave) && otherPawn.IsFreeNonSlaveColonist)
        {
            return "Master".Translate();
        }

        // Prisoner or slave labels
        if (otherPawn.IsPrisoner) return "Prisoner".Translate();
        if (otherPawn.IsSlave) return "Slave".Translate();

        // Hostile relationship
        if (pawn.Faction != null && otherPawn.Faction != null && pawn.Faction.HostileTo(otherPawn.Faction))
        {
            return "Enemy".Translate();
        }

        // No special status found
        return null;
    }

    private static bool TryGetSocialLabel(Pawn pawn, Pawn otherPawn, out string label, out float opinionValue)
    {
        label = null;
        opinionValue = 0f;

        try
        {
            opinionValue = pawn.relations.OpinionOf(otherPawn);
        }
        catch (Exception)
        {
            return false;
        }

        var mostImportantRelation = pawn.GetMostImportantRelation(otherPawn);
        if (mostImportantRelation != null)
        {
            label = mostImportantRelation.GetGenderSpecificLabelCap(otherPawn);
        }

        if (string.IsNullOrEmpty(label))
        {
            label = GetStatusLabel(pawn, otherPawn);
        }

        if (string.IsNullOrEmpty(label) && !pawn.IsVisitor() && !pawn.IsEnemy())
        {
            if (opinionValue >= FriendOpinionThreshold)
            {
                label = "Friend".Translate();
            }
            else if (opinionValue <= RivalOpinionThreshold)
            {
                label = "Rival".Translate();
            }
            else
            {
                label = "Acquaintance".Translate();
            }
        }

        return !string.IsNullOrEmpty(label);
    }
}
