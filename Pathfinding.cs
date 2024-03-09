// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using Yarl2;

// My implementation of Djisktra Maps, as defined at RogueBasin. Bsaically
// a flood fill that'll find the shortest paths from a given goal(s)
class DjikstraMap
{
    Map _map { get; set; }
    int _loRow { get; set; }
    int _hiRow { get; set; }
    int _loCol { get; set; }
    int _hiCol { get; set; }
    int[,]? _djikstraMap { get; set; }

    public DjikstraMap(Map map, int lowRow, int highRow, int loCol, int hiCol)
    {
        _map = map;
        _loRow = lowRow;
        _hiRow = highRow;
        _loCol = loCol;
        _hiCol = hiCol;
    }

    // Passable defines the squares to be used in the pathfinding and their weight
    // (Ie., a floor might be passable with score 1 but a door is 2 because it's 
    // slightly more expensive)
    // I'm going to make life easy on myself for now and just work with a 
    // single goal.
    public void Generate(Dictionary<TileType, int> passable, (int, int) goal)
    {
        int height = _hiRow - _loRow;
        int width = _hiCol - _loCol;
        _djikstraMap = new int[height, width];

        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                _djikstraMap[r, c] = int.MaxValue;
            }
        }

        // Mark the goal square
        int goalRow = goal.Item1 - _loRow;
        int goalCol = goal.Item2 - _loCol;
        _djikstraMap[goalRow, goalCol] = 0;

        var q = new Queue<(int, int)>();
        foreach (var sq in Util.Adj4Sqs(goalRow, goalCol))
        {
            if (sq.Item1 >= 0 && sq.Item2 >= 0 && sq.Item1 < height && sq.Item2 < width)
                q.Enqueue(sq);
        }
        HashSet<(int, int)> visited = [(goalRow, goalCol)];

        while (q.Count > 0)
        {
            var sq = q.Dequeue();
            if (visited.Contains(sq))
                continue;
            var tile = _map.TileAt(sq.Item1 + _loRow, sq.Item2 + _loCol);

            if (!passable.TryGetValue(tile.Type, out int cost))
                continue;

            int cheapestNeighbour = int.MaxValue;
            foreach (var n in Util.Adj4Sqs(sq.Item1, sq.Item2))
            {
                if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= height || n.Item2 >= width) 
                    continue;
                if (_djikstraMap[n.Item1, n.Item2] < cheapestNeighbour)
                    cheapestNeighbour = _djikstraMap[n.Item1, n.Item2];
                if (!visited.Contains(n))
                    q.Enqueue(n);
            }
            _djikstraMap[sq.Item1, sq.Item2] = cheapestNeighbour + cost;
            visited.Add(sq);
        }

        // for (int r = 0; r < height; r++)
        // {
        //     for (int c = 0; c < width; c++)
        //     {
        //         if (_djikstraMap[r, c] < int.MaxValue)
        //             Console.Write(_djikstraMap[r, c].ToString().PadRight(4));                
        //         else
        //             Console.Write("    ");
        //     }
        //     Console.WriteLine();
        // }        
    }

    public List<(int, int)> ShortestPath(int row, int col, int offsetRow, int offsetCol)
    {
        int height = _djikstraMap!.GetLength(0);
        int width = _djikstraMap.GetLength(1);
        List<(int, int)> path = [ (row, col) ];
        int currRow = row - offsetRow;
        int currCol = col - offsetCol;

        if (currRow < 0 || currCol < 0) 
        {
            return [];
        }

        int score = _djikstraMap[currRow, currCol];

        while (score != 0)
        {
            int cost = int.MaxValue;
            (int, int) next = (-1, -1);
            foreach (var adj in Util.Adj4Sqs(currRow, currCol))
            {
                if (adj.Item1 < 0 || adj.Item2 < 0 || adj.Item1 >= height || adj.Item2 >= width)
                    continue;
                if (_djikstraMap[adj.Item1, adj.Item2] < cost)
                {
                    next = (adj.Item1, adj.Item2);
                    cost = _djikstraMap[adj.Item1, adj.Item2];
                }
            }
            path.Add((next.Item1 + offsetRow, next.Item2 + offsetCol));
            score = cost;
            (currRow, currCol) = next;
        }

        return path;
    }

    public List<(int, int, int)> Neighbours(int row, int col)
    {
        List<(int, int, int)> adj = [];

        foreach (var (nr, nc) in Util.Adj8Sqs(row, col))
        {
            if (_djikstraMap[nr, nc] < int.MaxValue)
                adj.Add((nr, nc, _djikstraMap[nr, nc]));
        }

        return [.. adj.OrderBy(v => v.Item3)];
    }
}
