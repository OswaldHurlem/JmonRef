using System.Collections.Immutable;
using LibJmon.Linq;
using LibJmon.SuperTypes;
using LibJmon.Types;

namespace LibJmon.Impl;

public readonly record struct Assignment(ConvertedPath Path, JsonVal.Any Value);

public static class Assignments
{
    readonly record struct ConvertPathsState(IDictionary<ConvertedPath, int> IdxForPartialPath)
    {
        public static ConvertPathsState Initial => new(new Dictionary<ConvertedPath, int>());
    }

    private static ConvertedPath
        ConvertPath(ConvertedPath prefixPath, LexedPath lexedPath, IDictionary<ConvertedPath, int> idxForPartialPath)
    {
        var cvtPathElmts = prefixPath.V.ToList();

        PathItem.Idx ConvertProtoIdxElmt(PathItem.Idx arrElmt)
        {
            ConvertedPath partialPath = cvtPathElmts.ToImmutableArray();
            if (!idxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }

            idx += arrElmt.V;
            idxForPartialPath[partialPath] = idx;
            return idx;
        }

        var pathSegments = lexedPath.V.Segment(elmt => elmt is PathItem.Idx);

        foreach (IReadOnlyList<PathItem> pathSegment in pathSegments)
        {
            var elmt0 = pathSegment[0].AsOneOf().Match<PathItem>(keyElmt => keyElmt, ConvertProtoIdxElmt);
            cvtPathElmts.Add(elmt0);
            cvtPathElmts.AddRange(pathSegment.Skip(1));
        }

        return new ConvertedPath(cvtPathElmts.ToImmutableArray());
    }

    public static IReadOnlyList<Assignment> ComputeAssignments(AstNode head)
    {
        Dictionary<ConvertedPath, int> idxForPartialPath = new();

        IEnumerable<Assignment> Inner(ConvertedPath parentPath, AstNode node) =>
            node.AsOneOf().Match(
                leaf => new[] { new Assignment(parentPath, leaf) },
                branch => branch.V.SelectMany(
                    item => Inner(ConvertPath(parentPath, item.Path, idxForPartialPath), item.Node)
                ),
                error => throw new Exception("Asdf") // TODO
            );

        return Inner(ConvertedPath.Empty, head).ToList();
    }


   // private static bool FindStrayCell(LexedSubSheet sheet, CellKind kind, Coord firstCoord, out AstRslt.Error stray) =>
   //     sheet.Find(firstCoord, cell => cell.Is(kind))
   //         .MapFound(coord => AstRslt.MakeStrayCell(kind, sheet.ToOuter(coord)))
   //         .TryPickT0(out stray, out _);
}