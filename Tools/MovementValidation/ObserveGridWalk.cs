using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Game.Domain;
using Game.Application;
using Game.Presentation;
using Game.Views;
using ClassicSpins.PrototypeFramework.Domain;

// Bounded runtime observation, invoked through Unity-MCP. It is not a PlayMode test suite.
public static class ObserveGridWalk
{
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static BoardPresenter presenter;
    static BuddyView buddy;
    static LevelSession session;
    static Vector3[] points;
    static readonly List<object> samples = new List<object>();
    static float started, duration, pauseAt, maximumError;
    static bool paused;
    static string tap;

    public static string Main()
    {
        var scope=UnityEngine.Object.FindFirstObjectByType<Game.Composition.GameplayScreenScope>();
        if(scope==null)return "Gameplay is not ready";
        var screen=(GameplayScreen)scope.Container.Resolve(typeof(GameplayScreen));
        presenter=(BoardPresenter)typeof(GameplayScreen).GetField("_presenter",Private).GetValue(screen);
        var instance=(GameObject)typeof(GameplayScreen).GetField("_boardInstance",Private).GetValue(screen);
        var view=instance.GetComponent<BoardView>();
        var data=new Game.Infrastructure.ResourcesLevelCatalog().GetLevel(8);
        int tubeIndex=-1;IReadOnlyList<GridPos> route=null;BuddyMove move=default;
        for(int i=0;i<data.Tubes.Count;i++)
        {
            var probe=new Board(data);var result=probe.TapTube(i);
            if(!result.Accepted||result.State!=BoardState.Playing||result.Moves.Count==0)continue;
            var candidate=BoardWalkRoute.Build(probe,result.Moves[0].From,result.Moves[0].To);
            bool turn=false;
            for(int j=1;j+1<candidate.Count;j++)
                if(candidate[j-1].X!=candidate[j+1].X&&candidate[j-1].Y!=candidate[j+1].Y){turn=true;pauseAt=j*MotionTokens.GridStepDuration+.025f;break;}
            if(!turn)continue;
            tubeIndex=i;route=candidate;move=result.Moves[0];break;
        }
        if(tubeIndex<0)return "No bounded turn fixture found in level 8";
        session=new LevelSession(8,data,new Pcg32(735),new Dictionary<Booster,int>{{Booster.Undo,3},{Booster.Shuffle,3},{Booster.AddContainer,3}});
        // Preview state only: bypass StartLevel, BeginLevel, completion and save/progression operations.
        typeof(GameplayScreen).GetField("_session",Private).SetValue(screen,session);
        typeof(GameplayScreen).GetField("_data",Private).SetValue(screen,data);
        typeof(GameplayScreen).GetField("_level",Private).SetValue(screen,8);
        presenter.GuidedTube=-1;
        presenter.Show(session);
        var buddies=(Dictionary<int,BuddyView>)typeof(BoardView).GetField("_buddies",Private).GetValue(view);
        buddy=buddies[move.BuddyId];
        var at=typeof(BoardView).GetMethod("BuddyAt",Private);
        points=route.Select(p=>(Vector3)at.Invoke(view,new object[]{new Vector2Int(p.X,p.Y)})).ToArray();
        duration=(points.Length-1)*MotionTokens.GridStepDuration;
        Physics2D.SyncTransforms();
        started=Time.time;
        tap=TapSim.TapAt(TapSim.ScreenOf(buddy.transform.Find("Hit").position));
        if(session.MovesMade!=1)return "Pointer did not trigger the move: "+tap;
        // A second tap while walking must not mutate the next logical board state.
        TapSim.TapAt(TapSim.ScreenOf(buddy.transform.Find("Hit").position));
        if(session.MovesMade!=1)throw new InvalidOperationException("Walking input gate failed");
        Directory.CreateDirectory("docs/validation/grid-walk-20261125");
        File.WriteAllText("docs/validation/grid-walk-20261125/expected.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{level=8,tube=tubeIndex,id=move.BuddyId,duration,grid=route,points=points.Select(v=>new[]{v.x,v.y,v.z}).ToArray()},Newtonsoft.Json.Formatting.Indented));
        EditorApplication.update+=Observe;
        return "STARTED: level 8, pointer="+tap+", grid edges="+(points.Length-1)+", pause at first corner";
    }

    static void Observe()
    {
        if(!EditorApplication.isPlaying||buddy==null){EditorApplication.update-=Observe;return;}
        float elapsed=Time.time-started;
        Vector3 position=buddy.transform.localPosition;
        float error=float.MaxValue;
        for(int i=1;i<points.Length;i++)
        {
            Vector3 edge=points[i]-points[i-1];float t=Mathf.Clamp01(Vector3.Dot(position-points[i-1],edge)/edge.sqrMagnitude);
            error=Mathf.Min(error,Vector3.Distance(position,points[i-1]+edge*t));
        }
        maximumError=Mathf.Max(maximumError,error);
        samples.Add(new{elapsed,position=new[]{position.x,position.y,position.z},error});
        if(!paused&&elapsed>=pauseAt&&elapsed<duration)
        {
            paused=true;
            EditorApplication.isPaused=true;
        }
        if(elapsed<duration+.25f)return;
        EditorApplication.update-=Observe;
        int walking=(int)typeof(BoardPresenter).GetField("_walking",Private).GetValue(presenter);
        File.WriteAllText("docs/validation/grid-walk-20261125/observed.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{level=8,pointer=tap,duration,samples=samples.ToArray(),maximumError,arrived=Vector3.Distance(position,points[points.Length-1])<.001f,moves=session.MovesMade,walking,passed=maximumError<.001f&&session.MovesMade==1&&Vector3.Distance(position,points[points.Length-1])<.001f},Newtonsoft.Json.Formatting.Indented));
    }
}
