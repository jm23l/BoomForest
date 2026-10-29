using System;
using System.Collections.Generic;
namespace BoomForest
{
    public struct Tile : IEquatable<Tile>
    {
        public int x,y;public Tile(int x,int y){this.x=x;this.y=y;}
        public bool Equals(Tile b){return x==b.x&&y==b.y;}
        public override bool Equals(object b){return b is Tile&&Equals((Tile)b);}
        public override int GetHashCode(){return x*397^y;}
        public static Tile operator +(Tile a,Tile b){return new Tile(a.x+b.x,a.y+b.y);}
    }
    public sealed class Bomb {public Tile tile;public float fuse;public int id;}
    public sealed class Flame {public Tile tile;public float remaining;}
    public sealed class Slime {public Tile tile;public int id;}
    public sealed class ForestBoard
    {
        public const int Size=9, BombLimit=2, BlastRange=2;
        public const float FuseSeconds=2.2f;
        public readonly HashSet<Tile> Walls=new HashSet<Tile>(),Wood=new HashSet<Tile>();
        public readonly List<Bomb> Bombs=new List<Bomb>();
        public readonly List<Flame> Flames=new List<Flame>();
        public readonly List<Slime> Enemies=new List<Slime>();
        public Tile Player {get;private set;}
        public int Health {get;private set;}
        public int Score {get;private set;}
        public float Remaining {get;private set;}
        public float Invincible {get;private set;}
        public bool Over {get{return Health<=0||Remaining<=0||Enemies.Count==0;}}
        public bool Won {get{return Health>0&&Remaining>0&&Enemies.Count==0;}}
        public int Explosions {get;private set;}
        public int Hits {get;private set;}
        public int Kills {get;private set;}
        public int Broken {get;private set;}
        int nextBomb;float enemyClock;int stage;Random random;
        public static readonly Tile[] Directions={new Tile(0,-1),new Tile(1,0),new Tile(0,1),new Tile(-1,0)};
        public void Start(int stageIndex,int seed)
        {
            stage=stageIndex;random=new Random(seed);Walls.Clear();Wood.Clear();Bombs.Clear();Flames.Clear();Enemies.Clear();
            Health=3;Score=0;Remaining=75;Invincible=0;Explosions=Hits=Kills=Broken=nextBomb=0;enemyClock=1.5f;Player=new Tile(1,1);
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++){
                Tile p=new Tile(x,y);
                if(x==0||y==0||x==Size-1||y==Size-1||(x%2==0&&y%2==0))Walls.Add(p);
                else if(Math.Abs(x-1)+Math.Abs(y-1)>3&&random.NextDouble()<.36)Wood.Add(p);
            }
            Tile[] spawns={new Tile(7,7),new Tile(7,1),new Tile(1,7),new Tile(5,5),new Tile(3,7)};
            for(int i=0;i<Math.Min(3+stage,spawns.Length);i++){
                Tile p=spawns[i];Wood.Remove(p);foreach(Tile d in Directions)Wood.Remove(p+d);Enemies.Add(new Slime{tile=p,id=i});
            }
        }
        public bool IsFloor(Tile t){return t.x>=0&&t.y>=0&&t.x<Size&&t.y<Size&&!Walls.Contains(t);}
        public bool HasBomb(Tile t){foreach(Bomb b in Bombs)if(b.tile.Equals(t))return true;return false;}
        bool Walkable(Tile t){return IsFloor(t)&&!Wood.Contains(t)&&!HasBomb(t);}
        bool IsBurning(Tile t){foreach(Flame f in Flames)if(f.tile.Equals(t)&&f.remaining>0)return true;return false;}
        public bool Move(int dx,int dy)
        {
            if(Over||Math.Abs(dx)+Math.Abs(dy)!=1)return false;
            Tile n=Player+new Tile(dx,dy);if(!Walkable(n))return false;Player=n;Damage();return true;
        }
        public bool PlaceBomb()
        {
            if(Over||Bombs.Count>=BombLimit||HasBomb(Player))return false;
            Bombs.Add(new Bomb{tile=Player,fuse=FuseSeconds,id=nextBomb++});return true;
        }
        public void Tick(float dt)
        {
            if(Over||dt<=0)return;dt=Math.Min(dt,.1f);Remaining=Math.Max(0,Remaining-dt);Invincible=Math.Max(0,Invincible-dt);
            for(int i=Flames.Count-1;i>=0;i--){Flames[i].remaining-=dt;if(Flames[i].remaining<=0)Flames.RemoveAt(i);}
            foreach(Bomb b in Bombs)b.fuse-=dt;
            // Process one bomb at a time: newly triggered bombs are handled in this same tick.
            for(int guard=0;guard<32;guard++){
                int ready=Bombs.FindIndex(b=>b.fuse<=0);if(ready<0)break;
                Bomb bomb=Bombs[ready];Bombs.RemoveAt(ready);Explode(bomb.tile);
            }
            for(int i=Enemies.Count-1;i>=0;i--)if(IsBurning(Enemies[i].tile)){Enemies.RemoveAt(i);Kills++;Score+=100;}
            Damage();if(Over)return;
            enemyClock-=dt;if(enemyClock<=0){enemyClock=.70f-stage*.09f;MoveEnemies();Damage();}
        }
        void Burn(Tile p)
        {
            Flame f=Flames.Find(v=>v.tile.Equals(p));if(f==null)Flames.Add(new Flame{tile=p,remaining=.45f});else f.remaining=.45f;
            foreach(Bomb b in Bombs)if(b.tile.Equals(p))b.fuse=0;
        }
        void Explode(Tile p)
        {
            Explosions++;Burn(p);
            foreach(Tile d in Directions)for(int distance=1;distance<=BlastRange;distance++){
                Tile n=new Tile(p.x+d.x*distance,p.y+d.y*distance);if(!IsFloor(n))break;
                Burn(n);
                if(Wood.Remove(n)){Broken++;Score+=20;break;}
                if(HasBomb(n))break;
            }
        }
        void Damage()
        {
            if(Invincible>0||Health<=0)return;bool hit=IsBurning(Player);
            foreach(Slime e in Enemies)if(e.tile.Equals(Player)){hit=true;break;}
            if(hit){Health--;Hits++;Invincible=1.25f;}
        }
        void MoveEnemies()
        {
            foreach(Slime enemy in Enemies){
                var choices=new List<Tile>();
                foreach(Tile d in Directions){Tile n=enemy.tile+d;if(!Walkable(n)||IsBurning(n))continue;
                    bool occupied=false;foreach(Slime other in Enemies)if(other!=enemy&&other.tile.Equals(n))occupied=true;
                    if(!occupied)choices.Add(n);
                }
                if(choices.Count==0)continue;
                // Mix approaching the player and patrolling, so enemies remain readable.
                if(random.NextDouble()<.65){choices.Sort((a,b)=>(Math.Abs(a.x-Player.x)+Math.Abs(a.y-Player.y)).CompareTo(Math.Abs(b.x-Player.x)+Math.Abs(b.y-Player.y)));enemy.tile=choices[0];}
                else enemy.tile=choices[random.Next(choices.Count)];
            }
        }
    }
}
