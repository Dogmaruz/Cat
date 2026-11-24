using System;
namespace UnityEngine {
 public static class Mathf {
  public const float PI=(float)Math.PI;
  public static float Pow(float a,float b)=>(float)Math.Pow(a,b);
  public static float Max(float a,float b)=>Math.Max(a,b);
  public static float Min(float a,float b)=>Math.Min(a,b);
  public static float Abs(float a)=>Math.Abs(a);
  public static float Clamp(float x,float a,float b)=>Math.Min(b,Math.Max(a,x));
  public static float Sin(float x)=>(float)Math.Sin(x);
 }
}
namespace DancingCats.Clone.Editor {
 public static class IslandPilotJobs { public const string ReviewPath="C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/IslandPilot/PhoneInputScalarReview"; }
}
class Program { static void Main() { DancingCats.Clone.Editor.IslandSteeringChecks.RunChecks(); Console.WriteLine("PHONE_INPUT_SCALAR_CHECKS_PASS"); } }
