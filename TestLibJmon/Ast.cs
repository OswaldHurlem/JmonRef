using System.Text.Json;
using LibJmon.Impl;
using LibJmon.Types;

namespace TestLibJmon;

public static partial class TestRsrc
{
    public const string JmonSampleNoAppend = """
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
}

public static class Ast
{


    [Fact]
    public static void Asdf()
    {
        ReadOnlyMemory<byte>[,] cells = CsvUtil.MakeCells(TestRsrc.JmonSampleNoAppend, "|");
        LexedCell[,] lexedCells = TestingApi.LexCells(cells);
        AstNode ast = TestingApi.ParseLexedCells(lexedCells);
        var json = JsonSerializer.Serialize(ast, LibJmon.JsonSerialization.Resources.JsonSerializerOptions);
        Console.WriteLine(json);
    }
}