using System.Text.Json;
using LibJmon.Impl;
using LibJmon.Types;

namespace TestLibJmon;

public static class Assignment
{
    [Fact]
    public static void TempTest3()
    {
        ReadOnlyMemory<byte>[,] cells = CsvUtil.MakeCells(TestRsrc.JmonSampleNoAppend, "|");
        LexedCell[,] lexedCells = TestingApi.LexCells(cells);
        AstNode ast = TestingApi.ParseLexedCells(lexedCells);
        var parsedVal = LibJmon.TestingApi.AstToJson(ast);
        JsonSerializerOptions optionsButPretty = new(LibJmon.JsonSerialization.Resources.JsonSerializerOptions)
        {
            WriteIndented = true,
            
        };
        var json = JsonSerializer.Serialize(parsedVal, optionsButPretty);
        Assert.Equal(expJsonPretty, json);
    }

    private const string expJsonPretty = """
        {
          "characters": {
            "f4c3z": {
              "alph": {
                "name": "Romulus Alpha",
                "model": {
                  "day": "alph_greyhound",
                  "night": "alph_hunter_mech"
                }
              },
              "bear": {
                "name": "Honey Bear",
                "model": {
                  "day": "bear_honeywagon",
                  "night": "bear_melee_mech"
                }
              },
              "owl": {
                "name": "Mooncry",
                "model": {
                  "night": "owl_flying_mech"
                }
              }
            },
            "h33lz": {
              "nosf": {
                "name": "Nosferaudio",
                "model": {
                  "day": "nosf_speaker",
                  "night": "nosf_sound_mech"
                }
              },
              "boss": {
                "name": "Megacula",
                "model": {
                  "day": "boss_hearse",
                  "night": "boss_old_mech",
                  "cloud": "boss_cloud",
                  "disguised": "boss_disguised",
                  "reborn": "boss_card",
                  "final_form": "boss_final_form",
                  "defeated": "boss_defeated"
                }
              }
            }
          },
          "models": {
            "alph_greyhound": {
              "name": "Greyhound",
              "animations": [
                {
                  "id": "roll_out",
                  "speed": 1.3
                }
              ]
            },
            "alph_hunter_mech": {
              "name": "Hunter Mech",
              "animations": [
                {
                  "id": "sword_swing"
                },
                {
                  "id": "stake_missile"
                },
                {
                  "id": "equip_garlic",
                  "speed": 1.2
                },
                {
                  "id": "heroic_sacrifice"
                }
              ]
            },
            "bear_honeywagon": {
              "name": "Honeywagon",
              "animations": [
                {
                  "id": "liquid_spill"
                }
              ]
            },
            "bear_melee_mech": {
              "name": "Bear Mech",
              "animations": [
                {
                  "id": "scratch",
                  "speed": 0.5
                }
              ]
            },
            "nosf_speaker": {
              "name": "Smart Speaker",
              "animations": [
                {
                  "id": "eavesdrop"
                }
              ]
            },
            "nosf_sound_mech": {
              "name": "Disco Mech",
              "animations": [
                {
                  "id": "dance",
                  "speed": 0.9
                }
              ]
            },
            "owl_flying_mech": {
              "name": "Owl Mech",
              "animations": [
                {
                  "id": "scratch",
                  "speed": 0.7
                }
              ]
            },
            "boss_hearse": {
              "name": "Hearse",
              "animations": [
                {
                  "id": "stealthy_drive"
                }
              ]
            },
            "boss_old_mech": {
              "name": "Creepy Old Mech",
              "animations": [
                {
                  "id": "siphon_gas",
                  "speed": 1.5
                }
              ]
            },
            "boss_cloud": {
              "name": "Smog Cloud",
              "animations": [
                {
                  "id": "billow",
                  "speed": 1.4
                }
              ]
            },
            "boss_disguised": {
              "name": "Coup\u00E9 de Ville",
              "animations": [
                {
                  "id": "invite_passenger"
                }
              ]
            },
            "boss_card": {
              "name": "Galvacard",
              "animations": [
                {
                  "id": "sweep_cape",
                  "speed": 0.8
                }
              ]
            },
            "boss_final_form": {
              "name": "Final Form",
              "animations": [
                {
                  "id": "breathe_fire"
                }
              ]
            },
            "boss_defeated": {
              "name": "Megacula\u0027s Remains",
              "animations": [
                {
                  "id": "crumble"
                }
              ]
            }
          }
        }
        """;
}