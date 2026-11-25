// Test-only stand-ins so ProceduralAudio's pure maths runs outside Unity.
namespace UnityEngine {
  public static class Mathf {
    public const float PI = (float)System.Math.PI;
    public static float Sin(float x)=>(float)System.Math.Sin(x); public static float Cos(float x)=>(float)System.Math.Cos(x);
    public static float Exp(float x)=>(float)System.Math.Exp(x); public static float Abs(float x)=>System.Math.Abs(x);
    public static float Sign(float x)=>x>=0?1f:-1f; public static float Floor(float x)=>(float)System.Math.Floor(x);
    public static int FloorToInt(float x)=>(int)System.Math.Floor(x); public static int CeilToInt(float x)=>(int)System.Math.Ceiling(x);
    public static float Max(float a,float b)=>System.Math.Max(a,b); public static float Min(float a,float b)=>System.Math.Min(a,b);
    public static int Max(int a,int b)=>System.Math.Max(a,b); public static int Min(int a,int b)=>System.Math.Min(a,b);
    public static float Clamp(float v,float a,float b)=>v<a?a:v>b?b:v; public static float Clamp01(float v)=>Clamp(v,0,1);
    public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
    public static float Repeat(float t,float l)=>Clamp(t-Floor(t/l)*l,0,l);
  }
  public class AudioClip { public static AudioClip Create(string n,int len,int ch,int f,bool s)=>new AudioClip(); public bool SetData(float[] d,int o)=>true; }
  public static class Resources { public static T Load<T>(string p) where T: class => null; }
}
