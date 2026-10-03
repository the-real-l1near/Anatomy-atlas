using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace TranslucentUIFX.Tests.Editor
{
 public sealed class GlassRoutingPerformanceTests
 {
  private static IEnumerator NextRenderedFrame()
  {
   int frame=Time.frameCount;
   double deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
   while(Time.frameCount==frame)
   {
    Assert.Less(UnityEditor.EditorApplication.timeSinceStartup,deadline,"Unity did not advance a game frame.");
    yield return null;
   }
  }
  [UnityTest] public IEnumerator AutomaticCameraTracksSwitchesAcrossFrames()
  {
   yield return new EnterPlayMode();
   bool previousBackground=Application.runInBackground;
   Application.runInBackground=true;
   var previous=Camera.allCameras;
   foreach(var item in previous) item.enabled=false;
   var root=new GameObject("Camera switching",typeof(RectTransform),typeof(Canvas));
   var firstObject=new GameObject("First view",typeof(Camera));
   var secondObject=new GameObject("Second view",typeof(Camera));
   var first=firstObject.GetComponent<Camera>();var second=secondObject.GetComponent<Camera>();
   first.tag="MainCamera";second.enabled=false;
   var pane=new GameObject("Pane",typeof(RectTransform));pane.transform.SetParent(root.transform,false);
   var glass=pane.AddComponent<TranslucentImageFX>();
   bool initial=false, switched=false, missing=false, recovered=false;
   var trace=new System.Text.StringBuilder();
   try
   {
    yield return NextRenderedFrame();initial=glass.ResolvedCaptureCamera==first; trace.AppendLine($"initial: frame={Time.frameCount} resolved={glass.ResolvedCaptureCamera?.name} active={Camera.allCamerasCount}");
    first.enabled=false;second.enabled=true;
    yield return NextRenderedFrame();switched=glass.ResolvedCaptureCamera==second; trace.AppendLine($"switched: frame={Time.frameCount} resolved={glass.ResolvedCaptureCamera?.name} active={Camera.allCamerasCount}");
    second.enabled=false;
    yield return NextRenderedFrame();missing=glass.ResolvedCaptureCamera==null; trace.AppendLine($"missing: frame={Time.frameCount} resolved={glass.ResolvedCaptureCamera?.name} active={Camera.allCamerasCount}");
    first.enabled=true;
    yield return NextRenderedFrame();recovered=glass.ResolvedCaptureCamera==first; trace.AppendLine($"recovered: frame={Time.frameCount} resolved={glass.ResolvedCaptureCamera?.name} active={Camera.allCamerasCount}");
   }
   finally
   {
    Object.Destroy(root);Object.Destroy(firstObject);Object.Destroy(secondObject);
    foreach(var item in previous) if(item!=null) item.enabled=true;
   }
   yield return null;Application.runInBackground=previousBackground;yield return new ExitPlayMode();
   Assert.IsTrue(initial && switched && missing && recovered,"Automatic routing must follow camera enable/disable and recover without assigning a reference.\n"+trace);
  }
  [UnityTest] public IEnumerator SharedCanvasRoutingBenchmark()
  {
   yield return new EnterPlayMode();
   bool previousBackground=Application.runInBackground;
   Application.runInBackground=true;
   var root=new GameObject("Routing benchmark",typeof(RectTransform),typeof(Canvas));
   var panes=new TranslucentImageFX[128];
   for(int i=0;i<panes.Length;i++) { var go=new GameObject("Pane",typeof(RectTransform));go.transform.SetParent(root.transform,false);panes[i]=go.AddComponent<TranslucentImageFX>();panes[i].InteractiveGlare=false; }
   var samples=new double[30];long allocated=0;var clock=new Stopwatch();Camera resolved=null;
   for(int frame=0;frame<35;frame++)
   {
    yield return NextRenderedFrame();
    long before=GC.GetAllocatedBytesForCurrentThread();clock.Restart();
    for(int repeat=0;repeat<4;repeat++) for(int i=0;i<panes.Length;i++) resolved=panes[i].ResolvedCaptureCamera;
    clock.Stop();if(frame>=5){samples[frame-5]=clock.Elapsed.TotalMilliseconds;allocated+=GC.GetAllocatedBytesForCurrentThread()-before;}
   }
   Array.Sort(samples);
   string label=File.Exists("/tmp/liquid-routing-profile-label")?File.ReadAllText("/tmp/liquid-routing-profile-label").Trim():"latest";
   File.WriteAllText("/tmp/liquid-routing-"+label+".txt",$"128 panes, 512 routing queries/frame, 30 measured frames. Median ms={samples[15]:F6}; max ms={samples[29]:F6}; measured GC bytes={allocated}; camera={resolved?.name}\n");
   Object.Destroy(root);yield return null;Application.runInBackground=previousBackground;yield return new ExitPlayMode();
   Assert.That(allocated,Is.EqualTo(0),"Routing should allocate no managed memory after warm-up.");
  }
 }
}
