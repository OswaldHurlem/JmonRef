namespace TestLibJmon;

public static class Temp
{
    [Fact]
    static void TestAssignments()
    {
        var grid = new[,]
        {
            { ""    , ".c"     , ".d.+"      , ".d.$"       },
            { ".a"  , ".A.C"   , ".A.D[0]"   , ".A.D[0]"    },
            { ".b.+", ".B[0].C", ".B[0].D[0]", ".B[0].D[0]" },
            { ".b.+", ".B[1].C", ".B[1].D[0]", ".B[1].D[0]" },
        };

        var expPaths = new[]
        {
            ".a.c", ".a.d[0]", ".a.d[0]", ".b[0].c", ".b[0].d[0]", ".b[0].d[0]", ".b[1].c", ".b[1].d[0]", ".b[1].d[0]"
        };
        
        var expVals = new[]
        {
            ".A.C", ".A.D[0]", ".A.D[0]", ".B[0].C", ".B[0].D[0]", ".B[0].D[0]", ".B[1].C", ".B[1].D[0]", ".B[1].D[0]"
        };
        
        var assignments = LibJmon.Impl.TEMP.JQAssignmentsFromSimpleMatrix(grid).ToList();
        var actualPaths = assignments.Select(t => t.path).ToArray();
        var actualVals = assignments.Select(t => t.val).ToArray();
        Assert.Equal(expPaths, actualPaths);
        Assert.Equal(expVals, actualVals);
    }
}