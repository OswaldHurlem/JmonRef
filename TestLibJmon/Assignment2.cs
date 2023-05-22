using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon.Impl;
using LibJmon.Types;

namespace TestLibJmon;

/*
public static class Assignment
{
    [Fact]
    public static void TempTest()
    {
        ReadOnlyMemory<byte>[,] cells = CsvUtil.MakeCells(TestRsrc.JmonSampleNoAppend, "|");
        LexedCell[,] lexedCells = TestingApi.LexCells(cells);
        AstNode ast = TestingApi.ParseLexedCells(lexedCells);
        var assignments = TestingApi.ComputeAssignments(ast);
        var jsonActual =
          JsonSerializer.Serialize(assignments, LibJmon.JsonSerialization.Resources.JsonSerializerOptions);
        var jsonExpected =
          JsonSerializer.Deserialize<IReadOnlyList<LibJmon.Impl.Assignment>>(
            TestRsrc.JmonSampleNoAppend_Assignments,
            LibJmon.JsonSerialization.Resources.JsonSerializerOptions
          );
        Assert.Equal(TestRsrc.JmonSampleNoAppend_Assignments_Formatted, jsonActual);
    }
    
    [Fact]
    public static void TempTest2()
    {
        ReadOnlyMemory<byte>[,] cells = CsvUtil.MakeCells(TestRsrc.JmonSampleNoAppend, "|");
        LexedCell[,] lexedCells = TestingApi.LexCells(cells);
        AstNode ast = TestingApi.ParseLexedCells(lexedCells);
        var assignments = TestingApi.ComputeAssignments(ast);
        var json = TestingApi.MakeJsonFromAssignments(assignments);
        var asdf = json.ToJsonString();
        Console.WriteLine(asdf);
    }
}

public static partial class TestRsrc
{
    public static string JmonSampleNoAppend_Assignments_Formatted =>
      JsonSerializer.Serialize(
            JsonSerializer.Deserialize<JsonArray>(JmonSampleNoAppend_Assignments)
        );
  
    public const string JmonSampleNoAppend_Assignments = """
    [
      {
        "Path": [
          "characters",
          "f4c3z",
          "alph",
          "name"
        ],
        "Value": "Romulus Alpha"
      },
      {
        "Path": [
          "characters",
          "f4c3z",
          "alph",
          "model",
          "day"
        ],
        "Value": "alph_greyhound"
      },
      {
        "Path": [
          "characters",
          "f4c3z",
          "alph",
          "model",
          "night"
        ],
        "Value": "alph_hunter_mech"
      },
      {
        "Path": [
          "characters",
          "h33lz",
          "nosf",
          "name"
        ],
        "Value": "Nosferaudio"
      },
      {
        "Path": [
          "characters",
          "h33lz",
          "nosf",
          "model",
          "day"
        ],
        "Value": "nosf_speaker"
      },
      {
        "Path": [
          "characters",
          "h33lz",
          "nosf",
          "model",
          "night"
        ],
        "Value": "nosf_sound_mech"
      },
      {
        "Path": [
          "characters",
          "f4c3z",
          "bear",
          "name"
        ],
        "Value": "Honey Bear"
      },
      {
        "Path": [
          "characters",
          "f4c3z",
          "bear",
          "model",
          "day"
        ],
        "Value": "bear_honeywagon"
      },
      {
        "Path": [
          "characters",
          "f4c3z",
          "bear",
          "model",
          "night"
        ],
        "Value": "bear_melee_mech"
      },
      {
        "Path": [
          "characters",
          "f4c3z",
          "owl",
          "name"
        ],
        "Value": "Mooncry"
      },
      {
        "Path": [
          "characters",
          "f4c3z",
          "owl",
          "model",
          "night"
        ],
        "Value": "owl_flying_mech"
      },
      {
        "Path": [
          "characters",
          "h33lz",
          "boss",
          "name"
        ],
        "Value": "Megacula"
      },
      {
        "Path": [
          "characters",
          "h33lz",
          "boss",
          "model",
          "day"
        ],
        "Value": "boss_hearse"
      },
      {
        "Path": [
          "characters",
          "h33lz",
          "boss",
          "model",
          "night"
        ],
        "Value": "boss_old_mech"
      },
      {
        "Path": [
          "models",
          "alph_greyhound",
          "name"
        ],
        "Value": "Greyhound"
      },
      {
        "Path": [
          "models",
          "alph_greyhound",
          "animations",
          0,
          "id"
        ],
        "Value": "roll_out"
      },
      {
        "Path": [
          "models",
          "alph_greyhound",
          "animations",
          0,
          "speed"
        ],
        "Value": 1.3
      },
      {
        "Path": [
          "models",
          "alph_hunter_mech",
          "name"
        ],
        "Value": "Hunter Mech"
      },
      {
        "Path": [
          "models",
          "alph_hunter_mech",
          "animations",
          0,
          "id"
        ],
        "Value": "sword_swing"
      },
      {
        "Path": [
          "models",
          "alph_hunter_mech",
          "animations",
          1,
          "id"
        ],
        "Value": "stake_missile"
      },
      {
        "Path": [
          "models",
          "alph_hunter_mech",
          "animations",
          2,
          "id"
        ],
        "Value": "equip_garlic"
      },
      {
        "Path": [
          "models",
          "alph_hunter_mech",
          "animations",
          2,
          "speed"
        ],
        "Value": 1.2
      },
      {
        "Path": [
          "models",
          "alph_hunter_mech",
          "animations",
          3,
          "id"
        ],
        "Value": "heroic_sacrifice"
      },
      {
        "Path": [
          "models",
          "bear_honeywagon",
          "name"
        ],
        "Value": "Honeywagon"
      },
      {
        "Path": [
          "models",
          "bear_honeywagon",
          "animations",
          0,
          "id"
        ],
        "Value": "liquid_spill"
      },
      {
        "Path": [
          "models",
          "bear_melee_mech",
          "name"
        ],
        "Value": "Bear Mech"
      },
      {
        "Path": [
          "models",
          "bear_melee_mech",
          "animations",
          0,
          "id"
        ],
        "Value": "scratch"
      },
      {
        "Path": [
          "models",
          "bear_melee_mech",
          "animations",
          0,
          "speed"
        ],
        "Value": 0.5
      },
      {
        "Path": [
          "models",
          "nosf_speaker",
          "name"
        ],
        "Value": "Smart Speaker"
      },
      {
        "Path": [
          "models",
          "nosf_speaker",
          "animations",
          0,
          "id"
        ],
        "Value": "eavesdrop"
      },
      {
        "Path": [
          "models",
          "nosf_sound_mech",
          "name"
        ],
        "Value": "Disco Mech"
      },
      {
        "Path": [
          "models",
          "nosf_sound_mech",
          "animations",
          0,
          "id"
        ],
        "Value": "dance"
      },
      {
        "Path": [
          "models",
          "nosf_sound_mech",
          "animations",
          0,
          "speed"
        ],
        "Value": 0.9
      },
      {
        "Path": [
          "models",
          "owl_flying_mech",
          "name"
        ],
        "Value": "Owl Mech"
      },
      {
        "Path": [
          "models",
          "owl_flying_mech",
          "animations",
          0,
          "id"
        ],
        "Value": "scratch"
      },
      {
        "Path": [
          "models",
          "owl_flying_mech",
          "animations",
          0,
          "speed"
        ],
        "Value": 0.7
      },
      {
        "Path": [
          "models",
          "boss_hearse",
          "name"
        ],
        "Value": "Hearse"
      },
      {
        "Path": [
          "models",
          "boss_hearse",
          "animations",
          0,
          "id"
        ],
        "Value": "stealthy_drive"
      },
      {
        "Path": [
          "models",
          "boss_old_mech",
          "name"
        ],
        "Value": "Creepy Old Mech"
      },
      {
        "Path": [
          "models",
          "boss_old_mech",
          "animations",
          0,
          "id"
        ],
        "Value": "siphon_gas"
      },
      {
        "Path": [
          "models",
          "boss_old_mech",
          "animations",
          0,
          "speed"
        ],
        "Value": 1.5
      },
      {
        "Path": [
          "models",
          "boss_cloud",
          "name"
        ],
        "Value": "Smog Cloud"
      },
      {
        "Path": [
          "models",
          "boss_cloud",
          "animations",
          0,
          "id"
        ],
        "Value": "billow"
      },
      {
        "Path": [
          "models",
          "boss_cloud",
          "animations",
          0,
          "speed"
        ],
        "Value": 1.4
      },
      {
        "Path": [
          "models",
          "boss_disguised",
          "name"
        ],
        "Value": "Coupé de Ville"
      },
      {
        "Path": [
          "models",
          "boss_disguised",
          "animations",
          0,
          "id"
        ],
        "Value": "invite_passenger"
      },
      {
        "Path": [
          "models",
          "boss_card",
          "name"
        ],
        "Value": "Galvacard"
      },
      {
        "Path": [
          "models",
          "boss_card",
          "animations",
          0,
          "id"
        ],
        "Value": "sweep_cape"
      },
      {
        "Path": [
          "models",
          "boss_card",
          "animations",
          0,
          "speed"
        ],
        "Value": 0.8
      },
      {
        "Path": [
          "models",
          "boss_final_form",
          "name"
        ],
        "Value": "Final Form"
      },
      {
        "Path": [
          "models",
          "boss_final_form",
          "animations",
          0,
          "id"
        ],
        "Value": "breathe_fire"
      },
      {
        "Path": [
          "models",
          "boss_defeated",
          "name"
        ],
        "Value": "Megacula\u0027s Remains"
      },
      {
        "Path": [
          "models",
          "boss_defeated",
          "animations",
          0,
          "id"
        ],
        "Value": "crumble"
      }
    ]
    """;
}
*/