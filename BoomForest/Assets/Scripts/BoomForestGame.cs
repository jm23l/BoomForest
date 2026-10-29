using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
namespace BoomForest
{
    public sealed class BoomForestGame : MonoBehaviour
    {
        const string SaveKey="BoomForest_PC_v1_";const float CellSize=60;
        readonly Color dark=new Color32(22,42,41,255), green=new Color32(64,115,90,255), cream=new Color32(248,240,212,255), orange=new Color32(222,130,65,255);
        readonly ForestBoard model=new ForestBoard();
        readonly Dictionary<int,RectTransform> enemies=new Dictionary<int,RectTransform>(),bombs=new Dictionary<int,RectTransform>();
        readonly Dictionary<Tile,RectTransform> wood=new Dictionary<Tile,RectTransform>(),flames=new Dictionary<Tile,RectTransform>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        Font font;AudioSource audioSource;AudioClip explosion;
        RectTransform root,page,boardRoot,actor,overlay;Image hero;
        Text healthText,timeText,scoreText,enemyText,bombText,hintText;Button bombButton;
        bool running,paused,muted;int stage,bank,lastExplosions,lastHits;float nextMove,resultAt=-1,shakeUntil,hintUntil;
        Rect lastSafe;int lastWidth,lastHeight;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(FindFirstObjectByType<BoomForestGame>()==null)new GameObject("Boom Forest").AddComponent<BoomForestGame>();}
#if UNITY_EDITOR
        // Editor-only visual preview. No gameplay, sound or input system is started.
        public void CreateEditorPreview()
        {
            font=Resources.Load<Font>("Fonts/GameKorean");
            var cameraObject=new GameObject("Preview Camera");cameraObject.transform.SetParent(transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=dark;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.depth=-100;
            var canvas=new GameObject("Start Screen Preview",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            root=Panel(canvas.transform,"Desktop",0,0,1280,720,dark);root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.pivot=new Vector2(.5f,.5f);
            Resize();Menu();
            foreach(Transform child in GetComponentsInChildren<Transform>(true))child.gameObject.hideFlags=HideFlags.DontSave;
        }
        public void RefreshEditorPreview(){if(root!=null&&(Screen.safeArea!=lastSafe||Screen.width!=lastWidth||Screen.height!=lastHeight))Resize();}
#endif
        void Start()
        {
            Application.targetFrameRate=60;Screen.orientation=ScreenOrientation.LandscapeLeft;
            font=Resources.Load<Font>("Fonts/GameKorean");muted=PlayerPrefs.GetInt(SaveKey+"muted",0)==1;
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;MakeExplosion();
            Camera cam=Camera.main;if(cam==null){var c=new GameObject("Main Camera");cam=c.AddComponent<Camera>();c.tag="MainCamera";c.AddComponent<AudioListener>();}
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=dark;cam.orthographic=true;cam.transform.position=new Vector3(0,0,-10);
            if(EventSystem.current==null){var e=new GameObject("EventSystem");e.AddComponent<EventSystem>();e.AddComponent<InputSystemUIInputModule>();}
            var canvas=new GameObject("Game UI",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            root=Panel(canvas.transform,"Desktop",0,0,1280,720,dark);root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.pivot=new Vector2(.5f,.5f);Resize();Menu();
        }
        void Resize(){Rect s=Screen.safeArea;root.localScale=Vector3.one*Mathf.Min(s.width/1280f,s.height/720f);root.anchoredPosition=s.center-new Vector2(Screen.width,Screen.height)*.5f;lastSafe=s;lastWidth=Screen.width;lastHeight=Screen.height;}
        void Update()
        {
            if(root==null)return;if(Screen.safeArea!=lastSafe||lastWidth!=Screen.width||lastHeight!=Screen.height)Resize();
            if(Keyboard.current!=null&&Keyboard.current.f11Key.wasPressedThisFrame)Screen.fullScreen=!Screen.fullScreen;
            if(running&&paused&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Remove(ref overlay);paused=false;nextMove=Time.unscaledTime+.15f;return;}
            if(!running||paused)return;
            if(resultAt>=0){SyncViews();if(Time.unscaledTime>=resultAt){resultAt=-1;Result();}return;}
            Vector2 input=Vector2.zero;var k=Keyboard.current;
            if(k!=null){Vector2 keyboard=new Vector2((k.rightArrowKey.isPressed||k.dKey.isPressed?1:0)-(k.leftArrowKey.isPressed||k.aKey.isPressed?1:0),(k.upArrowKey.isPressed||k.wKey.isPressed?1:0)-(k.downArrowKey.isPressed||k.sKey.isPressed?1:0));if(keyboard.sqrMagnitude>0)input=keyboard;
                if(k.rKey.wasPressedThisFrame){StartStage(stage);return;}if(k.spaceKey.wasPressedThisFrame)DropBomb();if(k.escapeKey.wasPressedThisFrame){Pause();return;}}
            if(input.magnitude>.35f&&Time.unscaledTime>=nextMove){nextMove=Time.unscaledTime+.13f;int dx=0,dy=0;if(Mathf.Abs(input.x)>Mathf.Abs(input.y))dx=input.x>0?1:-1;else dy=input.y>0?-1:1;Face(dx,dy);model.Move(dx,dy);}
            model.Tick(Time.unscaledDeltaTime);
            if(model.Explosions!=lastExplosions){lastExplosions=model.Explosions;if(!muted)audioSource.PlayOneShot(explosion,.5f);shakeUntil=Time.unscaledTime+.16f;}
            if(model.Hits!=lastHits){lastHits=model.Hits;Sound("hit",.5f);Hint("맞았어요! 잠깐 반짝이는 동안 빠져나오세요.");}
            SyncViews();
            if(model.Over){resultAt=Time.unscaledTime+.55f;bombButton.interactable=false;}
        }
        void OnApplicationFocus(bool focus){if(!focus&&running&&!paused&&resultAt<0)Pause();}
        Sprite Art(string key){Sprite sp;if(!sprites.TryGetValue(key,out sp)){sp=Resources.Load<Sprite>("Sprites/"+key);sprites[key]=sp;}return sp;}
        RectTransform RectUI(Transform p,string name,float x,float y,float w,float h){var g=new GameObject(name,typeof(RectTransform));var r=g.GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        RectTransform Panel(Transform p,string name,float x,float y,float w,float h,Color c){var r=RectUI(p,name,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=c;im.raycastTarget=false;return r;}
        Text Label(Transform p,string str,float x,float y,float w,float h,int size,Color c,TextAnchor align=TextAnchor.MiddleLeft){var r=RectUI(p,"Label",x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=str;t.fontSize=size;t.color=c;t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        Image Image(Transform p,string key,float x,float y,float w,float h,Color? color=null){var r=RectUI(p,key,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.sprite=Art(key);im.color=color??Color.white;im.preserveAspect=true;im.raycastTarget=false;return im;}
        Button Button(Transform p,string str,float x,float y,float w,float h,Color c,Action action,int size=27){Panel(p,"Shadow",x,y+6,w,h,new Color32(12,31,29,255));var r=Panel(p,str,x,y,w,h,c);var im=r.GetComponent<Image>();im.raycastTarget=true;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=im;Label(r,str,4,0,w-8,h,size,cream,TextAnchor.MiddleCenter);b.onClick.AddListener(()=>{Sound("button",.3f);action();});return b;}
        void Remove(ref RectTransform r){if(r!=null){r.gameObject.SetActive(false);Destroy(r.gameObject);r=null;}}
        void Sound(string key,float volume=.4f){if(muted)return;var clip=Resources.Load<AudioClip>("Audio/"+key);if(clip!=null)audioSource.PlayOneShot(clip,volume);}
        void MakeExplosion(){const int rate=22050;var random=new System.Random(57);float[] data=new float[6600];for(int i=0;i<data.Length;i++){float t=(float)i/rate;data[i]=((float)random.NextDouble()*2-1)*Mathf.Exp(-t*19)*.55f+Mathf.Sin(t*2*Mathf.PI*(85-t*130))*Mathf.Exp(-t*14)*.3f;}explosion=AudioClip.Create("Explosion",data.Length,1,rate,false);explosion.SetData(data,0);}
        void Menu()
        {
            running=false;paused=false;resultAt=-1;Remove(ref overlay);Remove(ref page);
            page=Panel(root,"PC menu",0,0,1280,720,dark);
            for(int i=0;i<8;i++)Image(page,"tree",i*170-15,570,130,150);
            Label(page,"도트 폭탄 액션",75,70,650,50,28,new Color32(181,206,153,255));
            Label(page,"펑펑 숲",70,138,690,120,90,cream);
            Image(page,"hero_down",125,310,110,130);DrawBomb(page,308,342,70);DrawSlime(page,470,350,67);
            Label(page,"폭탄을 놓고, 폭발을 피하고,\n슬라임을 모두 물리치세요!",75,466,650,90,30,cream);
            Button(page,"게임 시작",810,170,360,80,orange,()=>{bank=0;StartStage(0);},32);
            Button(page,"게임 종료",810,276,360,65,green,Quit,27);
            Label(page,"WASD / 방향키   이동\nSpace   폭탄 설치\nEsc   일시정지    R   구역 재시작\nF11   전체 화면 전환",810,380,410,155,25,cream);
            Label(page,"3개 구역  ·  최고 기록 "+PlayerPrefs.GetInt(SaveKey+"best",0)+"점",75,637,680,45,25,cream);
        }
        void Quit(){
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void StartStage(int index)
        {
            Remove(ref overlay);Remove(ref page);stage=index;model.Start(stage,739+stage*127);enemies.Clear();bombs.Clear();wood.Clear();flames.Clear();lastExplosions=lastHits=0;resultAt=-1;nextMove=0;running=true;paused=false;
            page=Panel(root,"PC arena",0,0,1280,720,dark);
            Label(page,"펑펑 숲",75,26,540,70,42,cream);
            boardRoot=Panel(page,"Forest",75,140,540,540,new Color32(83,124,88,255));DrawBoard();
            Label(page,(stage+1)+" / 3 구역",690,32,330,50,34,new Color32(228,182,105,255));
            Button(page,"일시정지",1050,36,160,50,green,Pause,23);
            timeText=Label(page,"",690,126,280,55,40,cream);healthText=Label(page,"",990,126,235,55,30,cream);
            enemyText=Label(page,"",690,207,490,45,30,cream);scoreText=Label(page,"",690,270,490,45,32,cream);
            hintText=Label(page,"갈색 상자는 폭탄으로 부술 수 있어요.",690,343,500,92,26,cream);hintUntil=Time.unscaledTime+5;
            bombButton=Button(page,"폭탄 설치  [Space]",690,463,490,76,orange,DropBomb,29);
            bombText=Label(page,"",690,555,490,40,25,cream);
            Label(page,"WASD / 방향키  이동     Esc  일시정지\nR  구역 재시작     F11  전체 화면",690,622,510,67,23,new Color32(181,206,153,255));
            Sound("start");SyncViews();
        }
        void DrawBoard()
        {
            for(int y=0;y<ForestBoard.Size;y++)for(int x=0;x<ForestBoard.Size;x++){
                Tile p=new Tile(x,y);float px=x*CellSize,py=y*CellSize;
                Panel(boardRoot,"Grass",px,py,CellSize,CellSize,(x+y)%2==0?new Color32(112,151,99,255):new Color32(124,158,108,255));
                if(model.Walls.Contains(p)){Panel(boardRoot,"Tree base",px,py,CellSize,CellSize,new Color32(61,100,73,255));Image(boardRoot,"tree",px+5,py,50,60);}
                else if(model.Wood.Contains(p))wood[p]=DrawCrate(boardRoot,px,py);
            }
            actor=RectUI(boardRoot,"Hero",model.Player.x*CellSize,model.Player.y*CellSize,CellSize,CellSize);hero=Image(actor,"hero_down",8,5,48,55);
            hero.rectTransform.pivot=new Vector2(.5f,.5f);hero.rectTransform.anchoredPosition=new Vector2(30,-30);
        }
        RectTransform DrawCrate(Transform parent,float x,float y){var r=Panel(parent,"Breakable wooden crate",x+6,y+8,56,54,new Color32(107,65,43,255));Panel(r,"Planks",5,5,46,44,new Color32(181,126,69,255));Panel(r,"Brace",7,20,42,8,new Color32(219,168,95,255));Panel(r,"Brace",23,7,8,40,new Color32(219,168,95,255));return r;}
        RectTransform DrawBomb(Transform parent,float x,float y,float size){var r=RectUI(parent,"Bomb",x,y,size,size);Image(r,"circle",size*.1f,size*.19f,size*.8f,size*.8f,new Color32(34,42,48,255));Panel(r,"Fuse",size*.49f,0,size*.09f,size*.3f,new Color32(225,179,88,255));Panel(r,"Spark",size*.46f,0,size*.18f,size*.13f,new Color32(255,218,114,255));Panel(r,"Shine",size*.25f,size*.34f,size*.13f,size*.13f,new Color32(139,159,156,255));return r;}
        RectTransform DrawSlime(Transform parent,float x,float y,float size){var r=RectUI(parent,"Slime",x,y,size,size);Image(r,"circle",0,size*.15f,size,size*.8f,new Color32(184,79,89,255));Panel(r,"Eye",size*.23f,size*.42f,size*.15f,size*.22f,cream);Panel(r,"Eye",size*.62f,size*.42f,size*.15f,size*.22f,cream);Panel(r,"Pupil",size*.27f,size*.48f,size*.07f,size*.12f,dark);Panel(r,"Pupil",size*.66f,size*.48f,size*.07f,size*.12f,dark);return r;}
        void Face(int x,int y){hero.sprite=Art(y<0?"hero_up":y>0?"hero_down":"hero_side");hero.rectTransform.localScale=new Vector3(x<0?-1:1,1,1);}
        void DropBomb(){if(!running||paused||resultAt>=0||model.Over)return;if(model.PlaceBomb()){Sound("warning",.22f);Hint("폭탄 설치! 두 칸 밖이나 모퉁이 뒤로 피하세요.");}else Hint("설치된 폭탄이 터진 뒤 다시 놓을 수 있어요.");}
        void Hint(string str){if(hintText!=null)hintText.text=str;hintUntil=Time.unscaledTime+2.6f;}
        void SyncViews()
        {
            timeText.text=Mathf.CeilToInt(model.Remaining)+"초";timeText.color=model.Remaining<15?orange:cream;healthText.text="체력 "+model.Health+" / 3";
            enemyText.text="남은 슬라임 "+model.Enemies.Count;scoreText.text=(bank+model.Score)+"점";bombText.text="폭탄 "+model.Bombs.Count+" / 2 설치 중";bombButton.interactable=!model.Over&&model.Bombs.Count<2;
            if(Time.unscaledTime>hintUntil)hintText.text="슬라임을 모두 물리치면 다음 구역으로!";
            actor.anchoredPosition=Vector2.MoveTowards(actor.anchoredPosition,Point(model.Player),Time.unscaledDeltaTime*600);hero.color=model.Invincible>0&&Mathf.Sin(Time.unscaledTime*33)>0?new Color32(255,135,118,150):Color.white;
            boardRoot.anchoredPosition=new Vector2(75,-140)+(Time.unscaledTime<shakeUntil?new Vector2(Mathf.Sin(Time.unscaledTime*110)*3,Mathf.Cos(Time.unscaledTime*130)*3):Vector2.zero);
            foreach(Tile t in new List<Tile>(wood.Keys))if(!model.Wood.Contains(t)){Destroy(wood[t].gameObject);wood.Remove(t);}
            var enemyIds=new HashSet<int>();foreach(Slime e in model.Enemies){enemyIds.Add(e.id);RectTransform view;if(!enemies.TryGetValue(e.id,out view)){view=DrawSlime(boardRoot,e.tile.x*CellSize+10,e.tile.y*CellSize+10,48);enemies[e.id]=view;}view.anchoredPosition=Vector2.MoveTowards(view.anchoredPosition,Point(e.tile)+new Vector2(10,-10),Time.unscaledDeltaTime*300);}
            foreach(int id in new List<int>(enemies.Keys))if(!enemyIds.Contains(id)){Destroy(enemies[id].gameObject);enemies.Remove(id);}
            var bombIds=new HashSet<int>();foreach(Bomb b in model.Bombs){bombIds.Add(b.id);RectTransform view;if(!bombs.TryGetValue(b.id,out view)){view=DrawBomb(boardRoot,b.tile.x*CellSize+10,b.tile.y*CellSize+9,48);bombs[b.id]=view;}float pulse=b.fuse<.7f?1+Mathf.Sin(Time.unscaledTime*30)*.06f:1;view.localScale=Vector3.one*pulse;}
            foreach(int id in new List<int>(bombs.Keys))if(!bombIds.Contains(id)){Destroy(bombs[id].gameObject);bombs.Remove(id);}
            var fireTiles=new HashSet<Tile>();foreach(Flame f in model.Flames){fireTiles.Add(f.tile);if(!flames.ContainsKey(f.tile)){var v=Panel(boardRoot,"Blast",f.tile.x*CellSize+3,f.tile.y*CellSize+3,62,62,new Color32(239,134,48,220));Panel(v,"Flame core",13,13,36,36,new Color32(255,226,125,255));flames[f.tile]=v;}}
            foreach(Tile t in new List<Tile>(flames.Keys))if(!fireTiles.Contains(t)){Destroy(flames[t].gameObject);flames.Remove(t);}
            actor.SetAsLastSibling();
        }
        Vector2 Point(Tile t){return new Vector2(t.x*CellSize,-t.y*CellSize);}
        void Pause(){if(!running||paused||resultAt>=0)return;paused=true;Remove(ref overlay);overlay=Panel(root,"Pause overlay",0,0,1280,720,new Color(0,0,0,.82f));overlay.GetComponent<Image>().raycastTarget=true;
            Label(overlay,"잠깐 쉬기",350,60,580,80,49,cream,TextAnchor.MiddleCenter);
            Button(overlay,"계속하기",405,177,470,68,green,()=>{Remove(ref overlay);paused=false;nextMove=Time.unscaledTime+.15f;});
            Button(overlay,"이 구역 다시 하기",405,269,470,68,orange,()=>StartStage(stage));
            Button(overlay,"처음 화면",405,361,470,68,green,Menu);
            var b=Button(overlay,muted?"효과음 꺼짐":"효과음 켜짐",405,453,470,60,green,()=>{});b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>{muted=!muted;PlayerPrefs.SetInt(SaveKey+"muted",muted?1:0);PlayerPrefs.Save();b.GetComponentInChildren<Text>().text=muted?"효과음 꺼짐":"효과음 켜짐";Sound("button");});
            Button(overlay,"게임 종료",405,538,470,60,green,Quit);
        }
        void Result(){running=false;Remove(ref overlay);bool won=model.Won;int total=bank+model.Score;int best=Math.Max(PlayerPrefs.GetInt(SaveKey+"best",0),total);PlayerPrefs.SetInt(SaveKey+"best",best);PlayerPrefs.Save();Sound(won?"win":"lose",.5f);
            overlay=Panel(root,"Result overlay",0,0,1280,720,new Color(0,0,0,.87f));overlay.GetComponent<Image>().raycastTarget=true;
            Label(overlay,won?(stage==2?"숲을 지켰어요!":"구역 클리어!"):(model.Health<=0?"쓰러졌어요!":"시간 종료!"),70,120,620,90,50,cream,TextAnchor.MiddleCenter);
            Label(overlay,(stage+1)+" / 3 구역  ·  "+total+"점\n슬라임 "+model.Kills+"마리 처치\n상자 "+model.Broken+"개 파괴",70,270,620,160,32,new Color32(218,205,164,255),TextAnchor.MiddleCenter);
            Label(overlay,"최고 기록 "+best+"점",70,480,620,50,28,cream,TextAnchor.MiddleCenter);
            if(won&&stage<2)Button(overlay,"다음 구역",770,245,400,84,orange,()=>{bank=total;StartStage(stage+1);},30);
            else Button(overlay,"처음부터 다시 도전",770,245,400,84,orange,()=>{bank=0;StartStage(0);},30);
            Button(overlay,"처음 화면",770,365,400,78,green,Menu);
        }
    }
}
