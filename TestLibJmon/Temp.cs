using System.Collections;
using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon;
using LibJmon.Impl;
using LibJmon.Types;

namespace TestLibJmon;

public static class Temp
{
    //[Fact]
    //static void TestAssignments()
    //{
    //    var grid = new[,]
    //    {
    //        { ""    , ".c"     , ".d.+"      , ".d.$"       },
    //        { ".a"  , ".A.C"   , ".A.D[0]"   , ".A.D[0]"    },
    //        { ".b.+", ".B[0].C", ".B[0].D[0]", ".B[0].D[0]" },
    //        { ".b.+", ".B[1].C", ".B[1].D[0]", ".B[1].D[0]" },
    //    };
    //
    //    var expPaths = new[]
    //    {
    //        ".a.c", ".a.d[0]", ".a.d[0]", ".b[0].c", ".b[0].d[0]",
    //        ".b[0].d[0]", ".b[1].c", ".b[1].d[0]", ".b[1].d[0]"
    //    };
    //    
    //    var expVals = new[]
    //    {
    //        ".A.C", ".A.D[0]", ".A.D[0]", ".B[0].C", ".B[0].D[0]",
    //        ".B[0].D[0]", ".B[1].C", ".B[1].D[0]", ".B[1].D[0]"
    //    };
    //    
    //    var assignments = LibJmon.Impl.TEMP.JQAssignmentsFromSimpleMatrix(grid).ToList();
    //    var actualPaths = assignments.Select(t => t.path).ToArray();
    //    var actualVals = assignments.Select(t => t.val).ToArray();
    //    Assert.Equal(expPaths, actualPaths);
    //    Assert.Equal(expVals, actualVals);
    //}

    // [Fact]
    // static void TestParseEmpty()
    // {
    //     var lexedCells = new LexedCell[,] { { } };
    //     var 
    // }
    /*[Fact]
    static void TestParseSimple()
    {
        var mtxHeader = new LexedCell.Header.Mtx(MtxKind.Obj, false);
        var pathA = new LexedPath(new[] { new InputPathElmt.Key(new JsonVal.Str("a")) });
        var pathB = new LexedPath(new[] { new InputPathElmt.Key(new JsonVal.Str("b")) });
        var pathACell = new LexedCell.Path(pathA);
        var pathBCell = new LexedCell.Path(pathB);
        var valCell = new LexedCell.Header.Val(new JsonVal.Str("h"));

        var lexedCells = new LexedCell[,]
        {
            { mtxHeader, pathACell },
            { pathBCell, valCell },
        };
        
        var subSheet = new LexedSubSheet(lexedCells, (0, 0), false);
        var actual = Logic.ParseJmon(subSheet).Match(result => result, _ => null) as AstResult.Node.Branch;
        Assert.NotNull(actual);
        Assert.IsType<AstResult.Node.Branch>(actual);
        
        var astVal = new AstResult.Node.Leaf(valCell.V);
        var branchA = new AstResult.Node.Branch(new[] { new AstResult.Node.Branch.Item(pathA, astVal) }.ToList());
        AstResult expected = new AstResult.Node.Branch(
            new[] { new AstResult.Node.Branch.Item(pathB, branchA)
            }.ToList());

        var comparer = new AstResultComparer();
        Assert.True(comparer.Equals(actual, expected));

        //var actualJson = LibJmon.JsonSerialization.Serialize(actual);
        //var expectedJson = LibJmon.JsonSerialization.Serialize(expected);
        //
        //Assert.Equivalent(actualJson, expectedJson);
    }*/

    // TODO can remove
}