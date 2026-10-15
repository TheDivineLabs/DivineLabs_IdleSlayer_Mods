using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace DivineLabs.IdleSlayer;
internal static class GameVisuals
{
    private static Sprite rounded,logo,star;
    internal static void Initialize()
    {
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){
            float dx=Math.Max(0,8-Math.Min(x+.5f,31.5f-x)),dy=Math.Max(0,8-Math.Min(y+.5f,31.5f-y));
            float alpha=Math.Clamp(8.5f-MathF.Sqrt(dx*dx+dy*dy),0,1);texture.SetPixel(x,y,new Color(1,1,1,alpha));
        }
        texture.Apply(false,true);rounded=Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));
        var starTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);
        var points=new Vector2[10];
        for(int i=0;i<10;i++){float a=-MathF.PI/2+i*MathF.PI/5,r=i%2==0?15:6;points[i]=new Vector2(16+MathF.Cos(a)*r,16+MathF.Sin(a)*r);}
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)starTexture.SetPixel(x,31-y,Inside(points,x+.5f,y+.5f)?Color.white:Color.clear);
        starTexture.Apply(false,true);star=Sprite.Create(starTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f));
        // Rasterize our own vector mark once; stripped Unity span overloads cannot decode PNG reliably.
        var brand=new Texture2D(128,128,TextureFormat.RGBA32,false);
        Vector2[] outer={new(30,30),new(62,30),new(86,54),new(86,74),new(62,98),new(30,98)};
        Vector2[] inner={new(47,47),new(47,81),new(55,81),new(69,67),new(69,61),new(55,47)};
        Vector2[] second={new(86,30),new(100,30),new(100,98),new(74,98),new(88,84),new(86,84)};
        for(int y=0;y<128;y++)for(int x=0;x<128;x++){
            bool d=Inside(outer,x+.5f,y+.5f)&&!Inside(inner,x+.5f,y+.5f),l=Inside(second,x+.5f,y+.5f);
            brand.SetPixel(x,127-y,l?new Color(.933f,.949f,.973f,1):d?new Color(.447f,.812f,1,1):Color.clear);
        }
        brand.Apply(false,true);logo=Sprite.Create(brand,new Rect(0,0,128,128),new Vector2(.5f,.5f));
    }
    private static bool Inside(Vector2[] polygon,float x,float y)
    {
        bool inside=false;
        for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++){
            var a=polygon[i];var b=polygon[j];
            if((a.y>y)!=(b.y>y)&&x<(b.x-a.x)*(y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside;
    }
    internal static void Round(Image image){image.sprite=rounded;image.type=Image.Type.Sliced;}
    internal static Image Star(Transform parent,float x,float y,float size)
    {
        var node=new GameObject("Favorite");var r=node.AddComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(size,size);
        var image=node.AddComponent<Image>();image.sprite=star;image.raycastTarget=false;return image;
    }
    internal static void Logo(Transform parent,float x,float y,float size)
    {
        var node=new GameObject("DivineLabs.Logo");var r=node.AddComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(size,size);
        var image=node.AddComponent<Image>();image.sprite=logo;image.raycastTarget=false;
    }
}