using System;
using System.Collections.Generic;
using System.Linq;
using LittleEmpires.Core;

public static class RoadPathChecks
{
    public static string Run()
    {
        var passed = new List<string>();
        Action<bool,string> check = (ok,name) => { if (!ok) throw new Exception(name); passed.Add(name); };
        var tiles = new List<IslandTile>();
        for (var z=0;z<8;z++) for (var x=0;x<8;x++)
            tiles.Add(new IslandTile(new GridPosition(x,z), x==7 ? TerrainKind.Water : TerrainKind.Grassland));
        var map = new IslandMap(8,8,tiles);
        var state = new GameState(stone:10);
        var line = RoadPaths.Between(new GridPosition(1,1),new GridPosition(5,1));
        check(map.Roads.BuildPath(state,line).Succeeded && map.Roads.Segments.Count==5,"straight route");
        check(map.Roads.BuildPath(state,line).Succeeded && map.Roads.Segments.Count==5,"existing road reuse");
        var xFirst = RoadPaths.Between(new GridPosition(1,1),new GridPosition(5,3));
        var zFirst = RoadPaths.Between(new GridPosition(1,1),new GridPosition(5,3),false);
        check(xFirst.Count==7 && zFirst.Count==7 && xFirst[1]!=zFirst[1],"both L corners");
        var count = map.Roads.Segments.Count;
        check(!map.Roads.BuildPath(state,RoadPaths.Between(new GridPosition(1,2),new GridPosition(7,2))).Succeeded && map.Roads.Segments.Count==count,"blocked route atomicity");
        check(!map.Roads.BuildPath(state,new[]{new GridPosition(1,2),new GridPosition(3,2)}).Succeeded,"disconnected route rejected");
        check(map.Roads.BuildPath(state,line,true).Succeeded && state.Get(Resource.Stone)==0,"five upgrades cost exactly ten stone");
        check(map.Roads.BuildPath(state,line,true).Succeeded && state.Get(Resource.Stone)==0,"stone reuse free");
        map.Roads.BuildPath(state,zFirst);
        count=map.Roads.Segments.Count;
        check(!map.Roads.BuildPath(state,zFirst,true).Succeeded && state.Get(Resource.Stone)==0 && map.Roads.Segments.Count==count && map.Roads.Segments[new GridPosition(1,2)]==RoadSurface.Dirt,"insufficient stone atomicity");
        check(map.Roads.AreConnected(new GridPosition(1,1),new GridPosition(5,3)),"four-way connectivity");
        check(map.Roads.BuildPath(state,new[]{new GridPosition(6,6)}).Succeeded,"single click");
        return string.Join("; ",passed);
    }
}
