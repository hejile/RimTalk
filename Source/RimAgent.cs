using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using RimTalk.Util;
using RimWorld;
using Unity.Burst.Intrinsics;
using Verse;

namespace RimAgent;

[DataContract]
public class GameDTO
{
    [DataMember]
    public List<PawnDTO> PlayerFactionMembers = new();
    [DataMember]
    public List<MapDTO> Maps = new();
}

[DataContract]
public class MapDTO
{
    [DataMember]
    public string MapName;
    [DataMember]
    public List<ItemDTO> Items = new();
    [DataMember]
    public List<string> ColonistIds = new();
    [DataMember]
    public List<PawnDTO> Animals = new();
    [DataMember]
    public List<RoomDTO> Rooms = new();
}

[DataContract]
public class ItemDTO
{
    [DataMember]
    public string Name;
    [DataMember]
    public int Count;
}

[DataContract]
public class PawnDTO
{
    [DataMember]
    public string Id;
    [DataMember]
    public string Name;
    [DataMember]
    public string Kind;
    [DataMember]
    public string Gender;
    [DataMember]
    public int Age;
    [DataMember]
    public string State;
    [DataMember]
    public List<string> Traits = new();
    [DataMember]
    public string Childhood;
    [DataMember]
    public string Adulthood;
    [DataMember]
    public float MoodLevel;
    [DataMember]
    public List<string> TopThoughts = new();
    [DataMember]
    public List<string> HealthStatus = new();
    [DataMember]
    public string Ideology;
}

[DataContract]
public class RoomDTO
{
    [DataMember]
    public string Role;
    [DataMember]
    public float Cleanliness;
    [DataMember]
    public float Beauty;
}

[DataContract]
public class LogEntryDTO
{
    [DataMember]
    public string Level;
    [DataMember]
    public string Message;
}

[DataContract]
public class RustTickResponse
{
    [DataMember]
    public List<LogEntryDTO> Logs = new();
}

[DataContract]
public class SettingsDTO
{
    [DataMember]
    public string ApiKey;
    [DataMember]
    public string Provider;
    [DataMember]
    public string Model;
}

public class RimAgent : GameComponent
{
    private Game _game;
    private int _tickCounter = 0;
    private const int UpdateDataSeconds = 5;

    public RimAgent(Game game)
    {
        _game = game;
        RustAgent.StartRust();
        int magicNumber = RustAgent.GetRustMagicNumber();
        Logger.Message("RimAgent initialized with magic number: " + magicNumber);
        UpdateSettings();
    }

    public void UpdateSettings()
    {
        var settings = RimTalk.Settings.Get();
        var activeConfig = settings.GetActiveConfig();
        
        SettingsDTO dto = new SettingsDTO 
        { 
            ApiKey = activeConfig?.ApiKey ?? "",
            Provider = activeConfig?.Provider.ToString() ?? "None",
            Model = settings.GetCurrentModel()
        };

        try
        {
            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(SettingsDTO));
            serializer.WriteObject(stream, dto);
            string json = Encoding.UTF8.GetString(stream.ToArray());
            RustAgent.UpdateSettings(json);
            Logger.Debug($"Settings updated and sent to Rust. Provider: {dto.Provider}, Model: {dto.Model}");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to serialize SettingsDTO: " + ex.Message);
        }
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();
        int ticksPerSecond = CommonUtil.GetTicksForDuration(1);
        if (ticksPerSecond == 0)
            return; // This can happen when gravship flying.
        _tickCounter++;
        if (_tickCounter >= UpdateDataSeconds * ticksPerSecond)
        {
            _tickCounter = 0;
            UpdateGameInfo();
            Logger.Debug("Game info updated and sent to Rust.");
        }
        if (_tickCounter % ticksPerSecond == 0)
        {
            TickRust();
        }
    }

    private void TickRust()
    {
        try
        {
            string jsonResponse = RustAgent.RustTick();
            if (string.IsNullOrEmpty(jsonResponse)) return;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse));
            var serializer = new DataContractJsonSerializer(typeof(RustTickResponse));
            var response = (RustTickResponse)serializer.ReadObject(stream);

            if (response?.Logs != null)
            {
                foreach (var log in response.Logs)
                {
                    switch (log.Level) {
                        case "ERROR":
                            Logger.Error("[RimAgent Rust] " + log.Message);
                            break;
                        case "WARN":
                            Logger.Warning("[RimAgent Rust] " + log.Message);
                            break;
                        case "DEBUG":
                            Logger.Debug("[RimAgent Rust] " + log.Message);
                            break;
                        default:
                            Logger.Message("[RimAgent Rust] " + log.Message);
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error in TickRust: " + ex.Message);
        }
    }

    public override void GameComponentUpdate()
    {
        base.GameComponentUpdate();
    }

    private PawnDTO CreatePawnInfo(Pawn pawn)
    {
        PawnDTO info = new PawnDTO
        {
            Id = pawn.ThingID,
            Name = pawn.LabelShort,
            Kind = pawn.kindDef?.label,
            Gender = pawn.gender.ToString(),
            Age = pawn.ageTracker.AgeBiologicalYears,
            State = pawn.CurJob?.def.label ?? "Idle"
        };

        // 1. Traits
        if (pawn.story?.traits != null)
        {
            foreach (var trait in pawn.story.traits.allTraits)
            {
                info.Traits.Add(trait.LabelCap);
            }
        }

        // 2. Backstory
        info.Childhood = pawn.story?.Childhood?.title;
        info.Adulthood = pawn.story?.Adulthood?.title;

        // 3. Mood and Thoughts
        if (pawn.needs?.mood != null)
        {
            info.MoodLevel = pawn.needs.mood.CurLevel;
            
            // Get top thoughts by impact
            var thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(thoughts);
            var topThoughts = thoughts
                .OrderByDescending(t => Math.Abs(t.MoodOffset()))
                .Take(5)
                .Select(t => t.LabelCap)
                .ToList();
            info.TopThoughts = topThoughts;
        }

        // 4. Health
        if (pawn.health != null)
        {
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.Visible)
                {
                    string partLabel = hediff.Part != null ? $" ({hediff.Part.Label})" : "";
                    info.HealthStatus.Add($"{hediff.LabelCap}{partLabel}");
                }
            }
        }

        // 5. Ideology
        if (ModsConfig.IdeologyActive && pawn.ideo?.Ideo != null)
        {
            info.Ideology = pawn.ideo.Ideo.name;
        }

        return info;
    }

    private void UpdateGameInfo()
    {
        if (Find.Maps == null || Find.ColonistBar == null) return;

        GameDTO gameDto = new GameDTO();

        // 1. Faction Members (Strategy B: ColonistBar)
        var allColonists = Find.ColonistBar.GetColonistsInOrder();
        foreach (Pawn colonist in allColonists)
        {
            gameDto.PlayerFactionMembers.Add(CreatePawnInfo(colonist));
        }

        // 2. Map Data
        foreach (Map map in Find.Maps)
        {
            MapDTO mapDto = new MapDTO();
            mapDto.MapName = map.GetUniqueLoadID();

            // Items
            if (map.resourceCounter != null)
            {
                foreach (var kvp in map.resourceCounter.AllCountedAmounts)
                {
                    mapDto.Items.Add(new ItemDTO { Name = kvp.Key.label, Count = kvp.Value });
                }
            }

            // Colonists (IDs only)
            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                mapDto.ColonistIds.Add(pawn.ThingID);
            }

            // Animals (Tamed by player on this map)
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
                {
                    mapDto.Animals.Add(CreatePawnInfo(pawn));
                }
            }

            // Rooms
            if (map.regionGrid != null)
            {
                foreach (Room room in map.regionGrid.AllRooms)
                {
                    if (room.PsychologicallyOutdoors) continue;
                    mapDto.Rooms.Add(new RoomDTO
                    {
                        Role = room.Role?.label ?? "Room",
                        Cleanliness = room.GetStat(RoomStatDefOf.Cleanliness),
                        Beauty = room.GetStat(RoomStatDefOf.Beauty)
                    });
                }
            }
            
            gameDto.Maps.Add(mapDto);
        }

        try 
        {
            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(GameDTO));
            serializer.WriteObject(stream, gameDto);
            string json = Encoding.UTF8.GetString(stream.ToArray());
            RustAgent.UpdateGameInfo(json);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to serialize GameDTO: " + ex.Message);
        }
    }
}

