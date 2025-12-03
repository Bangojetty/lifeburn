using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class GameData : MonoBehaviour
{
    public string versionText;
    public Dictionary<Tribe, Sprite> spellToColor = new();
    public Dictionary<Tribe, Sprite> creatureToColor = new();
    public Dictionary<Tribe, Sprite> abilityToColor = new();
    public Dictionary<Tribe, Sprite> decklistToColor = new();
    public Dictionary<Tribe, VideoClip> tribeToCastVideo = new();
    public Dictionary<TokenType, Sprite> tokenToBack = new();
    public bool accDataUpdated;
    public Sprite targetableHighlight;
    public Sprite playableHighlight;
    public Sprite castingHighlight;
    
    public GameObject cardTemplatePfb;
    public GameObject cardSlotPfb;

    
    public AccountData accountData;

    // Artworks
    public List<Sprite> allArtworks = new();
    
    // Token Artworks
    public Dictionary<int, Sprite> tokenArtById = new();
    public List<Sprite> allTokenArts = new();
    public List<int> tokenArtIds = new();
    
    
    // Token Backgrounds (full arts)
    public Sprite herbBackground;
    public Sprite stoneBackground;
    
    // Templates
    public Sprite goblinTemplate;
    public Sprite golemTemplate;
    public Sprite merfolkTemplate;
    public Sprite shadowTemplate;
    public Sprite treefolkTemplate;
    
    public Sprite goblinSpellTemplate;
    public Sprite golemSpellTemplate;
    public Sprite merfolkSpellTemplate;
    public Sprite shadowSpellTemplate;
    public Sprite treefolkSpellTemplate;
    
    public Sprite goblinAbilityTemplate;
    public Sprite golemAbilityTemplate;
    public Sprite merfolkAbilityTemplate;
    public Sprite shadowAbilityTemplate;
    public Sprite treefolkAbilityTemplate;

    public Sprite merfolkDeckTemplate;
    public Sprite goblinDeckTemplate;
    public Sprite treefolkDeckTemplate;
    public Sprite shadowDeckTemplate;
    public Sprite golemDeckTemplate;
    
    // other

    public Sprite faceDownTemplate;

    public VideoClip goblinCastVideo;
    public VideoClip golemCastVideo;
    public VideoClip merfolkCastVideo;
    public VideoClip shadowCastVideo;
    public VideoClip treefolkCastVideo;

    public Sprite keywordBlitz;
    public Sprite keywordDive;
    public Sprite keywordExhaust;
    public Sprite keywordHaunt;
    public Sprite keywordScorch;
    public Sprite keywordSpectral;
    public Sprite keywordSprout;
    public Sprite keywordTaunt;
    public Sprite keywordTrample;
    
    public List<CardDisplayData> allCardDisplayDatas = new();
    public Dictionary<int,CardDisplayData> allCardsDict = new();
    private ServerApi serverApi = new();
    
    public DeckData currentDeck;
    public MatchState matchState;
    public Dictionary<Keyword, Sprite> keywordImgDict = new();


    public Dictionary<Phase, string> phaseToAnimDict = new();
    
    // Functioning Toggle
    public List<int> functioningIds;
    
    
    // UI (shared between all scenes)
    public List<Disabler> panelStack = new();
    


    private void Awake() {
        //alpha access to all cards
        allCardDisplayDatas = serverApi.GetAllCards();
    }
    
    private void Start() {
        DontDestroyOnLoad(gameObject);
        PopTypeDictionaries();
        PopKeywordSprites();
        PopAllCards();
        PopPhaseAnims();
        PopArtworkDicts();
    }

    private void PopArtworkDicts() {
        // tokens
        for (int i = 0; i < allTokenArts.Count; i++) {
            tokenArtById.Add(tokenArtIds[i], allTokenArts[i]);
        }
    }
    

    private void PopPhaseAnims() {
        phaseToAnimDict.Add(Phase.Draw, "DrawToMain");
        phaseToAnimDict.Add(Phase.Main, "MainToCombat");
        phaseToAnimDict.Add(Phase.Combat, "CombatToDamage");
        phaseToAnimDict.Add(Phase.Damage, "DamageToSecondMain");
        phaseToAnimDict.Add(Phase.SecondMain, "SecondMainToEnd");
        phaseToAnimDict.Add(Phase.End, "EndToDraw");
    }

    private void PopTypeDictionaries() {
        //unit card backgrounds
        creatureToColor.Add(Tribe.Merfolk, merfolkTemplate);
        creatureToColor.Add(Tribe.Treefolk, treefolkTemplate);
        creatureToColor.Add(Tribe.Goblin, goblinTemplate);
        creatureToColor.Add(Tribe.Golem, golemTemplate);
        creatureToColor.Add(Tribe.Shadow, shadowTemplate);
        
        //spell card backgrounds
        spellToColor.Add(Tribe.Merfolk, merfolkSpellTemplate);
        spellToColor.Add(Tribe.Treefolk, treefolkSpellTemplate);
        spellToColor.Add(Tribe.Goblin, goblinSpellTemplate);
        spellToColor.Add(Tribe.Golem, golemSpellTemplate);
        spellToColor.Add(Tribe.Shadow, shadowSpellTemplate);
        
        //ability backgrounds (for stack objects)
        abilityToColor.Add(Tribe.Merfolk, merfolkAbilityTemplate);
        abilityToColor.Add(Tribe.Treefolk, treefolkAbilityTemplate);
        abilityToColor.Add(Tribe.Goblin, goblinAbilityTemplate);
        abilityToColor.Add(Tribe.Golem, golemAbilityTemplate);
        abilityToColor.Add(Tribe.Shadow, shadowAbilityTemplate);
        
        //deck list backgrounds
        decklistToColor.Add(Tribe.Merfolk, merfolkDeckTemplate);
        decklistToColor.Add(Tribe.Treefolk, treefolkDeckTemplate);
        decklistToColor.Add(Tribe.Goblin, goblinDeckTemplate);
        decklistToColor.Add(Tribe.Golem, golemDeckTemplate);
        decklistToColor.Add(Tribe.Shadow, shadowDeckTemplate);
        
        //casting animations(video clips)
        tribeToCastVideo.Add(Tribe.Merfolk, merfolkCastVideo);
        tribeToCastVideo.Add(Tribe.Treefolk, treefolkCastVideo);
        tribeToCastVideo.Add(Tribe.Goblin, goblinCastVideo);
        tribeToCastVideo.Add(Tribe.Golem, golemCastVideo);
        tribeToCastVideo.Add(Tribe.Shadow, shadowCastVideo);
        
        //tokens
        tokenToBack.Add(TokenType.Herb, herbBackground);
        tokenToBack.Add(TokenType.Stone, stoneBackground);
    }

    private void PopKeywordSprites() {
        keywordImgDict.Add(Keyword.Blitz, keywordBlitz);
        keywordImgDict.Add(Keyword.Dive, keywordDive);
        keywordImgDict.Add(Keyword.Exhaust, keywordExhaust);
        keywordImgDict.Add(Keyword.Haunt, keywordHaunt);
        keywordImgDict.Add(Keyword.Scorch, keywordScorch);
        keywordImgDict.Add(Keyword.Spectral, keywordSpectral);
        keywordImgDict.Add(Keyword.Sprout, keywordSprout);
        keywordImgDict.Add(Keyword.Taunt, keywordTaunt);
        keywordImgDict.Add(Keyword.Trample, keywordTrample);
    }
    
    private void PopAllCards() { 
        foreach(var c in allCardDisplayDatas) {
            allCardsDict.Add(c.id, c);
        }
    }
}
