using System.Text.Json;
using LibJmon;
using LibJmon.Impl;
using LibJmon.Types;

namespace TestLibJmon;

public static class Assignment
{
    [Fact]
    public static void TempTest3()
    {
        string[,] cells = CsvUtil.CsvToCells(TestRsrc.SampleJmon, "|");
        string json = ApiV0.ParseJmon(cells, new() { WriteIndented = true });
        Assert.Equal(TestRsrc.SampleJmonAsPrettyJson, json);
    }
}

public static class TestRsrc
{
    public const string SampleJmon = """
        :{         |.                |                  |                |                    |           |               |
        .characters|:{               |.name             |.model.day      |.model.night        |.model.+*  |               |
                   |.f4c3z.alph      |Romulus Alpha     |alph_greyhound  |alph_hunter_mech    |           |               |
                   |.h33lz.nosf      |Nosferaudio       |nosf_speaker    |nosf_sound_mech     |           |               |
                   |.f4c3z.bear      |Honey Bear        |bear_honeywagon |bear_melee_mech     |           |               |
                   |.f4c3z.owl       |Mooncry           |                |owl_flying_mech     |           |               |
                   |.h33lz.boss      |Megacula          |boss_hearse     |boss_old_mech       |:{         |.              |
                   |                 |                  |                |                    |.cloud     |boss_cloud     |
                   |                 |                  |                |                    |.disguised |boss_disguised |
                   |                 |                  |                |                    |.reborn    |boss_card      |
                   |                 |                  |                |                    |.final_form|boss_final_form|
                   |                 |                  |                |                    |.defeated  |boss_defeated  |
        .models    |:{               |.name             |.animations.+.id|.animations.$.speed |           |               |
                   |.alph_greyhound  |Greyhound         |roll_out        |::1.3               |           |               |
                   |.alph_hunter_mech|Hunter Mech       |sword_swing     |                    |           |               |
                   |.alph_hunter_mech|                  |stake_missile   |                    |           |               |
                   |.alph_hunter_mech|                  |equip_garlic    |::1.2               |           |               |
                   |.alph_hunter_mech|                  |heroic_sacrifice|                    |           |               |
                   |.bear_honeywagon |Honeywagon        |liquid_spill    |                    |           |               |
                   |.bear_melee_mech |Bear Mech         |scratch         |::0.5               |           |               |
                   |.nosf_speaker    |Smart Speaker     |eavesdrop       |                    |           |               |
                   |.nosf_sound_mech |Disco Mech        |dance           |::0.9               |           |               |
                   |.owl_flying_mech |Owl Mech          |scratch         |::0.7               |           |               |
                   |.boss_hearse     |Hearse            |stealthy_drive  |                    |           |               |
                   |.boss_old_mech   |Creepy Old Mech   |siphon_gas      |::1.5               |           |               |
                   |.boss_cloud      |Smog Cloud        |billow          |::1.4               |           |               |
                   |.boss_disguised  |Coupé de Ville    |invite_passenger|                    |           |               |
                   |.boss_card       |Galvacard         |sweep_cape      |::0.8               |           |               |
                   |.boss_final_form |Final Form        |breathe_fire    |                    |           |               |
                   |.boss_defeated   |Megacula's Remains|crumble         |                    |           |               |
        """;
    
    public const string SampleJmonAsPrettyJson = """
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