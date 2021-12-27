using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

public class BlueRatTASState {

    public string Log;
    public RbyTile Tile;
    public int EdgeSet;
    public int WastedFrames;
    public byte HRandomAdd;
    public byte HRandomSub;
    public byte RDiv;

    public override int GetHashCode() {
        var hash = new HashCode();
        hash.Add(Tile.X);
        hash.Add(Tile.Y);
        hash.Add(EdgeSet);
        hash.Add(WastedFrames);
        hash.Add(HRandomAdd);
        hash.Add(HRandomSub);
        hash.Add(RDiv);
        return hash.ToHashCode();
    }
}

// Code heavily plagiarized from: https://github.com/entrpntr/gb-rta-bruteforce/blob/master/src/dabomstew/rta/entei/GSToto.java
public static class BlueRatTAS {

    const int MaxCost = 16;
    static StreamWriter Writer;
    public static HashSet<int> seenStates = new HashSet<int>();

    public static void OverworldSearch(Rby gb, BlueRatTASState state) {
        if (!seenStates.Add(state.GetHashCode())) {
            return;
        }
        byte[] oldState = gb.SaveState();

        var edgeList = state.Tile.Edges[state.EdgeSet];
        foreach(var edge in edgeList) {
            gb.LoadState(oldState);
            if (edge.Cost + state.WastedFrames > MaxCost) continue;

            int ret = gb.Execute(edge.Action);
            if (ret == gb.SYM["CalcStats"])
            {
                if (gb.CpuRead("wEnemyMonSpecies") == gb.Species["RATTATA"].Id && gb.CpuRead("wEnemyMonLevel") == 5) {
                    int dvs = gb.CpuRead("wEnemyMonDVs") << 8 | gb.CpuRead(gb.SYM["wEnemyMonDVs"] + 1);

                    int atk = (dvs >> 12) & 0xf;
                    int def = (dvs >> 8) & 0xf;
                    int spd = (dvs >> 4) & 0xf;
                    int spc = dvs & 0xf;

                    if (atk == 15 && def < 7 && spd > 12 && spc >= 12) {
                        lock (Writer) {
                            var foundPoke = $"[{state.WastedFrames} cost] {state.Log}{edge.Action.LogString()} - 0x{dvs:x4}";
                            Writer.WriteLine(foundPoke);
                            Writer.Flush();
                            Console.WriteLine(foundPoke);
                        }
                    }
                }
                continue;
            }
            OverworldSearch(gb, new BlueRatTASState {
                Log = state.Log + edge.Action.LogString() + " ",
                Tile = edge.NextTile,
                EdgeSet = edge.NextEdgeset,
                WastedFrames = state.WastedFrames + edge.Cost,
                HRandomAdd = gb.CpuRead("hRandomAdd"),
                HRandomSub = gb.CpuRead("hRandomSub"),
                RDiv = gb.CpuRead(0xFF04)
            });
            gb.LoadState(oldState);
        }
    }

    public static void StartSearch(int numThreads = 6) {
        Blue dummyGb = new Blue();
        RbyMap viridianCityMap = dummyGb.Maps[1];
        RbyMap route2map = dummyGb.Maps[13];
        Pathfinding.GenerateEdges<RbyMap, RbyTile>(dummyGb, 0, viridianCityMap[17, 0], Action.Right | Action.Down | Action.Up | Action.Left | Action.A);
        Pathfinding.GenerateEdges<RbyMap, RbyTile>(dummyGb, 1, route2map[8, 48], Action.Right | Action.Down | Action.Up | Action.Left | Action.A);
        RbyTile startTile = viridianCityMap[19, 9];
        viridianCityMap[18, 0].AddEdge(0, new Edge<RbyMap, RbyTile>(){Action = Action.Up, NextTile = route2map[8, 71], NextEdgeset = 0, Cost = 0 });
        viridianCityMap[17, 0].AddEdge(0, new Edge<RbyMap, RbyTile>(){Action = Action.Up, NextTile = route2map[7, 71], NextEdgeset = 0, Cost = 0 });
        Writer = new StreamWriter("Blue Rat TAS" + DateTime.Now.Ticks + ".txt");
        
        for (int threadIndex = 0; threadIndex < numThreads; threadIndex++) {
            new Thread(parameter => {
                int index = (int)parameter;
                Blue gb = new Blue();
                Console.WriteLine("starting movie");
                gb.PlayBizhawkInputLog("movies/BlueRaticateTAS.txt");
                gb.SetSpeedupFlags(SpeedupFlags.NoSound | SpeedupFlags.NoVideo);
                Console.WriteLine("finished movie");
                gb.RunUntil("JoypadOverworld");
                for (int i = 0; i < index; i++) {
                    gb.AdvanceFrame();
                    gb.RunUntil("JoypadOverworld");
                }

                OverworldSearch(gb, new BlueRatTASState {
                    Log = $"thread {index} ",
                    Tile = startTile,
                    WastedFrames = 0,
                    EdgeSet = 0,
                    HRandomAdd = gb.CpuRead("hRandomAdd"),
                    HRandomSub = gb.CpuRead("hRandomSub"),
                    RDiv = gb.CpuRead(0xFF04)
                });
            }).Start(threadIndex);
        }
    }
}