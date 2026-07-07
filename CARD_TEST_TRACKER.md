# Card Test Tracker

**Total Cards:** 281
**Untested:** 180
**Passed:** 86
**Failed:** 15

---

## Status Legend
- **Untested** - Not yet tested
- **Passed** - Tested and working correctly
- **Failed** - Tested and has issues (see notes)
- **Fixed** - Was failed, now fixed and passed

---

## All Cards

| ID | Name | Tribe | Cost | Status | Round 1 Notes | Round 2 Notes |
|----|------|-------|------|--------|---------------|---------------|
| 0 | PerfectEarthBlessing | golem | 0 | Passed |  |  |
| 1 | GolemBlinker | golem | 0 | Passed | Optional trigger from hand, bot auto-decline fix |  |
| 2 | GolemThrower | golem | 0 | Passed | Choose effect with targeting |  |
| 3 | GolemTrampler | golem | 0 | Fixed | Trample, activated sacrifice |  |
| 4 | GoldGolem | golem | 0 | Passed |  |  |
| 5 | StoneSculptor | golem | 0 | Passed |  |  |
| 6 | Golem | golem | 0 | Passed |  |  |
| 7 | RockGolem | golem | 0 | Passed |  |  |
| 8 | GolemFounder | golem | 0 | Passed | Reveal effect with amountBasedOn |  |
| 9 | AlchemyGolem | golem | 0 | Passed |  |  |
| 10 | IronGolems | golem | 0 | Passed |  |  |
| 11 | TransparentGolem | golem | 0 | Passed |  |  |
| 12 | StoneShaper | golem | 0 | Passed | Choose effect with token creation |  |
| 13 | ReconfigureGolem | golem | 0 | Fixed | InZone condition trigger from graveyard |  |
| 14 | GolemSmasher | golem | 0 | Fixed | UI text duplication fix for granted passives |  |
| 15 | ReplacerGolem | golem | 0 | Passed | Fixed leftZone trigger (card already moved when checking) |  |
| 16 | ExcavatorGolem | golem | 0 | Passed |  |  |
| 17 | Smash | golem | 0 | Passed | X display, thisTurn buff, cancel fix |  |
| 237 | AvalancheGolem | golem | 0 | Passed | Variable stone sacrifice, playerChosenAmount, all damage |  |
| 240 | BreakThrough | golem | 0 | Passed | Attacking restriction, granted keywords, additionalEffects targetBasedOn |  |
| 241 | FoundationGolem | golem | 0 | Passed | phaseOfPlayer for controller-only trigger |  |
| 242 | GraniteGolem | golem | 0 | Passed |  |  |
| 243 | TargetDummy | golem | 0 | Passed | Taunt keyword implementation |  |
| 244 | ShatteringSmash | golem | 0 | Passed | requireOneFromEach targeting, BothPlayersHaveSummons cast restriction |  |
| 18 | GolemBlesser | golem | 1 | Fixed | Clone/owner system for self-buff fix |  |
| 19 | MoltenGravel | golem | 1 | Passed | Fixed conditional trample (GetKeywords now uses GetVerifiedPassives) |  |
| 20 | ChancellorGolem | golem | 1 | Passed | Fixed opening hand trigger zone check |  |
| 21 | EarthquakeGolem | golem | 1 | Passed |  |  |
| 22 | LordGolem | golem | 2 | Passed | Fixed aura (self: false in JSON) |  |
| 24 | RockArms | golem | 2 | Passed | Token buff fix |  |
| 25 | BrassGolem | golem | 2 | Passed | Fixed dynamic token stats (passive with amountBasedOn), baseAttack/baseDefense for color |  |
| 238 | CastAMold | golem | 2 | Passed |  |  |
| 23 | RockToss | golem | 3 | Fixed | Conditional damage modifier fix (effectOwner vs affectedPlayer) |  |
| 29 | Quarry | golem | 3 | Fixed | isPlayerTurn, description fix |  |
| 30 | DigUp | golem | 3 | Passed |  |  |
| 26 | FoundryGolem | golem | 4 | Fixed | ModifyCost passive with stone condition |  |
| 31 | Stones | golem | 4 | Passed |  |  |
| 33 | Stoned | golem | 4 | Passed |  |  |
| 37 | StoneSearch | golem | 4 | Passed | Fixed tutor-to-play event ordering (SendToZone before RefreshCardDisplays) |  |
| 32 | StoneToss | golem | 5 | Passed | Variable X sacrifice, X-based life cost, 2X damage |  |
| 239 | GroundTactics | golem | 5 | Fixed |  |  |
| 34 | MasterGolem | golem | 6 | Passed | Tribute multiplier, StonesInPlay amountBasedOn |  |
| 36 | DigitalStone | golem | 6 | Passed |  |  |
| 38 | Foundry | golem | 6 | Passed |  |  |
| 41 | StoneWall | golem | 7 | Passed |  |  |
| 35 | RockAvalanche | golem | 8 | Fixed |  |  |
| 40 | GolemGod | golem | 12 | Passed | Implemented alternate sacrifice cost |  |
| 42 | GamePlan | merfolk | 0 | Passed |  |  |
| 43 | MerfolkBalancer | merfolk | 0 | Passed |  |  |
| 44 | SeagateMerfolk | merfolk | 0 | Passed |  |  |
| 45 | Calculate | merfolk | 0 | Passed | Counter spell targeting stack items |  |
| 46 | ChancellorMerfolk | merfolk | 0 | Passed |  |  |
| 47 | RiverSiren | merfolk | 0 | Untested |  |  |
| 48 | EidolonOfTheTides | merfolk | 0 | Passed |  |  |
| 49 | PoseidonsBlessing | merfolk | 0 | Passed |  |  |
| 50 | Fishies | merfolk | 0 | Failed |  |  |
| 51 | MerfolkGazer | merfolk | 0 | Passed |  |  |
| 52 | DiverMerfolk | merfolk | 0 | Untested |  |  |
| 53 | MerfolkRusher | merfolk | 0 | Passed |  |  |
| 54 | CuriousMerfolk | merfolk | 0 | Passed |  |  |
| 55 | MerfolkKeeper | merfolk | 0 | Passed | Draw trigger with notFirst restriction, +1/+1 counters |  |
| 56 | MerfolkRevealer | merfolk | 0 | Failed | Self-sacrifice, reveal target selection |  |
| 57 | MerfolkScrollkeeper | merfolk | 0 | Passed |  |  |
| 59 | MerfolkGang | merfolk | 0 | Failed | Cast from hand trigger, optional, cost restrictions |  |
| 60 | MerfolkLeader | merfolk | 0 | Passed |  |  |
| 61 | MerfolkFatekeeper | merfolk | 0 | Passed |  |  |
| 62 | MerfolkDeceiver | merfolk | 0 | Passed | KeywordsOrAbilities restriction |  |
| 63 | Denial | merfolk | 0 | Failed |  |  |
| 236 | DreamBig | merfolk | 0 | Passed |  |  |
| 245 | PutridFolks | merfolk | 0 | Untested | ExileAndReturn with SendToZone |  |
| 247 | MerfolkFateseer | merfolk | 0 | Passed |  |  |
| 248 | BeachedMerfolk | merfolk | 0 | Passed |  |  |
| 250 | SlipspaceMerfolk | merfolk | 0 | Passed | Auto-sacrifice, targetType permanent |  |
| 252 | MerfolkMage | merfolk | 0 | Failed | Copy spell trigger, TriggeredEffect type, rootEffect clone fix |  |
| 254 | Typhoon | merfolk | 0 | Failed |  |  |
| 255 | Rewind | merfolk | 0 | Failed | GoToPhase event for turn restart |  |
| 64 | MerfolkShifter | merfolk | 1 | Passed |  |  |
| 65 | MerfolkFinder | merfolk | 1 | Passed |  |  |
| 66 | EadroMerfolkGod | merfolk | 1 | Failed | DiscardOrSacrificeMerfolk cost, thisTurn passives, self-sacrifice, death trigger tokens |  |
| 67 | MerfolkScoper | merfolk | 1 | Passed |  |  |
| 68 | MerfolkSummoner | merfolk | 1 | Failed |  |  |
| 253 | SkyScryerMerfolk | merfolk | 1 | Failed | TopCardRevealed passive, deck top card UI, click-to-cast |  |
| 70 | Dream | merfolk | 2 | Passed |  |  |
| 71 | MerfolkBase | merfolk | 2 | Passed |  |  |
| 72 | MerfolkSwarm | merfolk | 2 | Passed |  |  |
| 73 | MerfolkMaster | merfolk | 2 | Passed | self:false aura fix for innate passives |  |
| 74 | Consider | merfolk | 2 | Untested |  |  |
| 75 | SiftRubble | merfolk | 2 | Failed |  |  |
| 81 | Opt | merfolk | 2 | Failed | Fixed infinite loop with resolve index, optional shuffle |  |
| 246 | Riptide | merfolk | 2 | Passed | Token destruction includes Token class instances |  |
| 251 | MerfolkElite | merfolk | 2 | Failed |  |  |
| 76 | Snag | merfolk | 3 | Passed |  |  |
| 77 | BackSnap | merfolk | 3 | Untested | Spell alternate cost ordering fix (cost choice before target selection) |  |
| 78 | DrawCounter | merfolk | 3 | Passed | Counter target selection fix |  |
| 79 | Dispell | merfolk | 3 | Untested |  |  |
| 80 | MerfolkTribe | merfolk | 3 | Passed |  |  |
| 82 | Brainstorm | merfolk | 3 | Untested | Multiplayer flow fix, shuffle message fix |  |
| 83 | CounterBalance | merfolk | 3 | Untested |  |  |
| 256 | SpawnFish | merfolk | 3 | Passed |  |  |
| 84 | Return | merfolk | 4 | Untested | Each player effect |  |
| 85 | GodMerfolk | merfolk | 4 | Passed | Fixed multiply stat description (x2/x2 shows "doubles") |  |
| 86 | Shatter | merfolk | 4 | Passed |  |  |
| 87 | Shell | merfolk | 4 | Passed | Fixed CantBeTargeted passive description and targeting check |  |
| 88 | SnapShot | merfolk | 4 | Passed | Fixed allOfSameName clone, token inclusion |  |
| 89 | TimeTwist | merfolk | 5 | Untested | Fixed opponent hand visual sync, Spellburnt condition |  |
| 90 | CommandJustice | merfolk | 5 | Failed |  |  |
| 91 | Refresh | merfolk | 5 | Passed | Implemented modifySummonLimit effect |  |
| 92 | WashAway | merfolk | 8 | Passed |  |  |
| 249 | Legionaires | merfolk | 9 | Passed |  |  |
| 93 | TurnTime | merfolk | 10 | Untested | Fixed phaseOfPlayer trigger, forOpponentChoice text |  |
| 94 | GodRecallSpell | merfolk | 10 | Passed |  |  |
| 95 | SwapControl | merfolk | 15 | Failed |  |  |
| 96 | DuskWraith | shadow | 0 | Untested | Conditional destroy/gainLife, target selection message fix |  |
| 97 | Ghastly | shadow | 0 | Untested | Opening hand playerChoice discard |  |
| 98 | GraveDigger | shadow | 0 | Untested |  |  |
| 99 | LootGhost | shadow | 0 | Untested |  |  |
| 100 | HaunterShade | shadow | 0 | Untested | Fixed JSON: triggeredEffects, trigger mill, self true, createToken |  |
| 101 | WitnessShade | shadow | 0 | Untested | Fixed each player mill (two effects with isOpponent), description override |  |
| 103 | ShadeHerald | shadow | 0 | Untested |  |  |
| 104 | ShadowDancer | shadow | 0 | Untested |  |  |
| 105 | SelflessShadow | shadow | 0 | Untested | Tribute trigger, token with keywords |  |
| 106 | ShadowOfTheGrave | shadow | 0 | Untested | Innate passive scope fix |  |
| 107 | GhastlyTutor | shadow | 0 | Untested |  |  |
| 108 | ShadeCrawler | shadow | 0 | Untested | Graveyard trigger, targetBasedOn triggerCard |  |
| 109 | GhostReceiver | shadow | 0 | Untested | Mill upTo amount selection fix |  |
| 110 | ShadeRunner | shadow | 0 | Untested |  |  |
| 111 | GhostDeceiver | shadow | 0 | Untested |  |  |
| 112 | DarkBlessing | shadow | 0 | Untested |  |  |
| 113 | ShadeOfReturn | shadow | 0 | Untested |  |  |
| 114 | BluntAmbusher | shadow | 0 | Untested | Life-dependent passive stats refresh |  |
| 258 | RecurringNightmare | shadow | 0 | Untested | isPlayerTurn for draw phase trigger |  |
| 259 | SetStraight | shadow | 0 | Untested | SourcePlayer targetBasedOn, HalfLife amountBasedOn, any number selection |  |
| 261 | RestlessGhost | shadow | 0 | Untested |  |  |
| 102 | GhostGathering | shadow | 1 | Untested | Replacement effect (summons to exile), playerChoice castCard, mill trigger fix |  |
| 115 | DoubleShadow | shadow | 1 | Untested |  |  |
| 116 | RelentingShade | shadow | 1 | Untested | Trigger scope fix, resolution-time inZone condition |  |
| 117 | ThreeShadows | shadow | 1 | Untested |  |  |
| 118 | Shade | shadow | 1 | Untested |  |  |
| 119 | HandRefresh | shadow | 1 | Untested |  |  |
| 122 | LostButNeverGone | shadow | 1 | Untested |  |  |
| 257 | GeistOfDroolingTears | shadow | 1 | Untested |  |  |
| 120 | Reap | shadow | 2 | Untested | Opponent discard selection, player UID preservation fix |  |
| 121 | LingeringShades | shadow | 2 | Untested |  |  |
| 123 | GhostlyLooter | shadow | 2 | Untested |  |  |
| 124 | DarkShade | shadow | 2 | Untested | Discard trigger skip card qualification, bot auto-discard, oncePerTurn tracking |  |
| 128 | Edict | shadow | 2 | Untested | eachPlayer sacrifice handling |  |
| 125 | Fisher | shadow | 3 | Untested | targetType opponent fix, weakest summon targeting |  |
| 126 | Vanquish | shadow | 3 | Untested |  |  |
| 127 | ShadowLord | shadow | 3 | Untested | Graveyard aura fix, statModifiers typo, passive removal on zone change |  |
| 133 | Duress | shadow | 3 | Untested | resolveTarget castability fix, reveal only matching cards |  |
| 137 | CrawlBack | shadow | 3 | Untested | Fixed targetType to cardInGraveyard |  |
| 260 | Spectral Amulet | shadow | 3 | Untested | Object type card handling (no attack, no tribute) |  |
| 129 | Fable | shadow | 4 | Untested |  |  |
| 131 | Reaper | shadow | 4 | Untested | resolveTarget discard, Summon restriction, reveal filtering |  |
| 132 | Strongfall | shadow | 4 | Untested | targetType opponent, strongest summon targeting |  |
| 235 | Spectralize | shadow | 4 | Untested | ExileAndReturn with targeting, targetBasedOn rootAffected |  |
| 130 | ItsAlive | shadow | 5 | Untested |  |  |
| 134 | HauntGod | shadow | 6 | Untested | Tribute altCostType handling in CanPayAlternateCost, RequestActivatedAbilityAltCostPayment, HandleCostSelection |  |
| 135 | ExchangeSouls | shadow | 6 | Untested | resolveTarget for mill-then-select, targetType cardInGraveyard |  |
| 136 | Wrath | shadow | 11 | Untested | Implemented destroy all effect |  |
| 234 | RitualOfDarkness | shadow | 11 | Untested | CardRitualOfDarkness effect, SendToZone for Hand->Play, trigger deferral |  |
| 138 | ChainofBolts | goblin | 0 | Untested | opponentsChoice for resolve-time target selection by opponent |  |
| 139 | Gobby | goblin | 0 | Untested | Draw effect description for targetType opponent |  |
| 140 | LootingFire | goblin | 0 | Untested | resolveTarget for conditional targeting, Control condition default minAmount=1 |  |
| 141 | GobLaunch | goblin | 0 | Untested | AdditionalCost tribe/cardType check, sacrifice only from play, TargetAttack condition |  |
| 142 | ExploderGob | goblin | 0 | Untested | affectedPlayer for non-targeted damage, DealDamage effect description |  |
| 143 | SpearGob | goblin | 0 | Untested |  |  |
| 144 | BlitzGoblin | goblin | 0 | Untested |  |  |
| 145 | GobRocket | goblin | 0 | Untested |  |  |
| 146 | TransparentGoblin | goblin | 0 | Untested |  |  |
| 147 | GoblinDuelist | goblin | 0 | Untested | attackedSummon trigger, survivedCombat sacrifice |  |
| 148 | Maglubiyet'sBlessing | goblin | 0 | Untested |  |  |
| 149 | GobRunner | goblin | 0 | Untested |  |  |
| 150 | FuryGoblin | goblin | 0 | Untested |  |  |
| 151 | GoblinCrew | goblin | 0 | Untested |  |  |
| 152 | LooterGob | goblin | 0 | Untested | Cast trigger, isCost reveal selection |  |
| 153 | UndeadGoblin | goblin | 0 | Untested | ImmuneToKeyword (Dive, Trample, Haunt) |  |
| 154 | GoblinRitualist | goblin | 0 | Untested | CastCard from graveyard with select, free cast implementation |  |
| 155 | Shot | goblin | 0 | Untested | modifierConditions with control condition |  |
| 156 | FireMasterGob | goblin | 0 | Untested | Cast trigger with spellburnt conditions, tribe filter |  |
| 157 | FiringGoblin | goblin | 0 | Untested | isCost reveal with resolveTarget damage |  |
| 158 | GoblinSquadron | goblin | 0 | Untested | Token with tributeRestriction passive |  |
| 159 | BabyGobs | goblin | 0 | Untested | Token with tributeRestriction passive |  |
| 166 | GoblinGod | goblin | 0 | Untested | bypassSummonLimit, goblinsControlled amountBasedOn |  |
| 180 | Wildfire | goblin | 0 | Untested | DynamicAdd cost modifier, SummonsOpponentControls |  |
| 181 | Channel | goblin | 0 | Untested |  |  |
| 263 | GoblinGrunt | goblin | 0 | Untested |  |  |
| 264 | CavalcadePyromancer | goblin | 0 | Untested | Cast trigger cardType filter, TriggerController targetBasedOn |  |
| 267 | GoblinChanneler | goblin | 0 | Untested | Cost restriction for triggers, TriggerController targetBasedOn |  |
| 268 | SearingGoblin | goblin | 0 | Untested | DisableEnterPlayEffects passive |  |
| 269 | GoblinLieutenant | goblin | 0 | Untested |  |  |
| 271 | GoblinTactician | goblin | 0 | Untested | FinalAttack damage, proper targeting, option message |  |
| 160 | GoblinEngineer | goblin | 1 | Untested | Tutor with reveal |  |
| 161 | GoblinMomma | goblin | 1 | Untested | Dual token creation |  |
| 162 | GoblinPortal | goblin | 1 | Untested | Opening hand trigger, tribute with keyword filter |  |
| 163 | GoblinMaster | goblin | 1 | Untested | goblinsInPlay amountBasedOn passive |  |
| 164 | GoblinRally | goblin | 1 | Untested | Attack trigger, tokenAttacking |  |
| 165 | GoblinTrickster | goblin | 1 | Untested | Spell tutor with reveal |  |
| 167 | RallyTheMogs | goblin | 1 | Untested | Aura passives: changeStats + grantKeyword |  |
| 168 | ForkBolt | goblin | 1 | Untested |  |  |
| 262 | BurstLightning | goblin | 1 | Untested | selectRepeatUpfront, upfront life cost, skip invalid targets |  |
| 265 | Ringleader Champion | goblin | 1 | Untested | Grant passive option text fix |  |
| 272 | AllOutAttackCommander | goblin | 1 | Untested | CantSpecialSummon with selfOnly scope |  |
| 169 | ChieftanGob | goblin | 2 | Untested |  |  |
| 170 | Greed | goblin | 2 | Untested | Control condition with maxAmount:0 fix |  |
| 171 | HeatRay | goblin | 2 | Untested |  |  |
| 172 | GoblinTown | goblin | 2 | Untested | CreateToken amountModifier display fix |  |
| 270 | BlastOpen | goblin | 2 | Untested | DefenseGreaterThanAttack restriction |  |
| 173 | Smite | goblin | 3 | Untested |  |  |
| 175 | Gamble | goblin | 3 | Untested |  |  |
| 232 | Barrage | goblin | 3 | Untested |  |  |
| 233 | FlameWave | goblin | 4 | Untested |  |  |
| 176 | RunAmok | goblin | 5 | Untested | CantTribute passive implementation |  |
| 177 | HeatWave | goblin | 5 | Untested |  |  |
| 178 | Fireblast | goblin | 5 | Untested | Damage modifier with goblin control, alternate exile cost |  |
| 266 | SearingFire | goblin | 5 | Untested |  |  |
| 179 | Obliterate | goblin | 8 | Untested | Damage to both players, cantGainLife for both |  |
| 27 | TreeOfBurningFire | treefolk | 0 | Untested | GrantActive passive, token activation, self-sacrifice, deep copy Effect fix |  |
| 39 | Woad-Hollow | treefolk | 0 | Untested | Fixed mill trigger zone check |  |
| 58 | TreeOfSafeguard | treefolk | 0 | Untested | DisableKeyword aura, sacrifice self, reveal from hand |  |
| 69 | SpreadingThornbush | treefolk | 0 | Untested |  |  |
| 182 | PlantOfSolitude | treefolk | 0 | Untested | CantTribute passive, tribute trigger scope:all, opponent choice deep clone fix |  |
| 183 | EternalTreefolk | treefolk | 0 | Untested | Optional at trigger level, HasInZone condition |  |
| 184 | JeelaiPlant | treefolk | 0 | Untested |  |  |
| 185 | CipplingVines | treefolk | 0 | Untested | NonTreefolk restriction, TreefolkControlled amountBasedOn |  |
| 186 | PlantOfHerbs | treefolk | 0 | Untested |  |  |
| 187 | NaturesBlessing | treefolk | 0 | Untested |  |  |
| 188 | TreeSavant | treefolk | 0 | Untested |  |  |
| 189 | GrappleRoots | treefolk | 0 | Untested |  |  |
| 190 | SproutPlant | treefolk | 0 | Untested |  |  |
| 191 | VinePlant | treefolk | 0 | Untested |  |  |
| 192 | Treefice | treefolk | 0 | Untested |  |  |
| 193 | GiverOfPlants | treefolk | 0 | Untested |  |  |
| 194 | SproutAnArmy | treefolk | 0 | Untested | Herb sacrifice selection, cantGainLife choice text |  |
| 195 | Planter | treefolk | 0 | Untested |  |  |
| 196 | TallTreefolk | treefolk | 0 | Untested |  |  |
| 197 | NaturalStatePlant | treefolk | 0 | Untested | Graveyard ability selection fix, inspection panel autoPass fix |  |
| 198 | PlantGrower | treefolk | 0 | Untested | Phase trigger with attacked condition, scope selfOnly sacrifice |  |
| 199 | Sproutlings | treefolk | 0 | Untested |  |  |
| 200 | PlantSprouter | treefolk | 0 | Untested | Cast trigger, TreefolkControlled fix (exclude tokens) |  |
| 201 | PlatePlant | treefolk | 0 | Untested |  |  |
| 202 | CliffsideSprout | treefolk | 0 | Untested | phaseOfPlayer fix for controller-only draw trigger |  |
| 203 | GlowingSpore | treefolk | 0 | Untested | RootController fix for destroyed cards using lastControllingPlayer |  |
| 204 | DeadTree | treefolk | 0 | Untested | zone vs targetZones fix for non-targeting sendToZone |  |
| 205 | GiftOfNature | treefolk | 0 | Untested | targetType graveyard for target selection |  |
| 206 | TransparentPlant | treefolk | 0 | Untested | activateFromHand ability, targetType/tribe on inner effect |  |
| 228 | DeeprootGuard | treefolk | 0 | Untested |  |  |
| 230 | TreeOfLife | treefolk | 0 | Untested | Self-sacrifice, targetType cardInHand |  |
| 274 | EndlessGarden | treefolk | 0 | Untested | eventTriggers for delayed phase trigger on stack |  |
| 275 | UnendingSundew | treefolk | 0 | Untested | immediate ability, eventTriggers for delayed return |  |
| 276 | FromDust | treefolk | 0 | Untested | targetType for graveyard |  |
| 277 | PottedFlower | treefolk | 0 | Untested |  |  |
| 279 | GroundControl | treefolk | 0 | Untested |  |  |
| 28 | BoneToPeaches | treefolk | 1 | Untested | Fixed token targeting in GetPossibleTargets |  |
| 207 | PerfectBog | treefolk | 1 | Untested |  |  |
| 208 | TreeGiant | treefolk | 1 | Untested | DefenseUsedForAttack passive, activateFromHand discard ability |  |
| 209 | SpiritTree | treefolk | 1 | Untested |  |  |
| 210 | WarbriarStomper | treefolk | 1 | Untested |  |  |
| 280 | BloomingMarsh | treefolk | 1 | Untested | DidntAttack condition, phase trigger |  |
| 211 | VerdictCommand | treefolk | 2 | Untested | RemoveCounter effect for haunt counters |  |
| 212 | Entangle | treefolk | 2 | Untested | conditionMax/cardType fix for Control condition |  |
| 213 | Harvest | treefolk | 2 | Untested |  |  |
| 231 | Herblore | treefolk | 2 | Untested | BypassHerbLifeReduction player passive |  |
| 273 | UndyingDeathwood | treefolk | 2 | Untested | InZones condition, freeCast, TriggerSprout, scope all for mill |  |
| 278 | TreeOfAbundance | treefolk | 2 | Untested | TokenCanTribute, CreateTokenModifier (*2), token stacking fix |  |
| 214 | LostSanctuary | treefolk | 3 | Untested | Multiple choose effects, opponent choice handling |  |
| 215 | PlanterBox | treefolk | 3 | Untested | thisTurn token death |  |
| 216 | GrowTall | treefolk | 3 | Untested | IsTribe condition, targetBasedOn rootAffected |  |
| 217 | Fertilize | treefolk | 3 | Untested | Player passives for futureProof keyword grants |  |
| 218 | PlantofTrees | treefolk | 3 | Untested |  |  |
| 219 | SproutUp | treefolk | 3 | Untested |  |  |
| 220 | Overrun | treefolk | 4 | Untested |  |  |
| 222 | Simplify | treefolk | 4 | Untested |  |  |
| 226 | PlantSnap | treefolk | 4 | Untested |  |  |
| 227 | Gigatrunk | treefolk | 4 | Untested |  |  |
| 229 | MasterTree | treefolk | 4 | Untested | CantAttack passive |  |
| 221 | Uncover | treefolk | 5 | Untested |  |  |
| 225 | Grow | treefolk | 7 | Untested |  |  |
| 174 | ExplosiveVegetation | treefolk | 8 | Untested | TributeMultiplier for summons, ModifyType rootEffect.affectedUids filtering |  |
| 223 | TreeGod | treefolk | 9 | Untested |  |  |
| 224 | Green-Sun | treefolk | 9 | Untested |  |  |

---

## Test Session Log

### Session 1 - [Date]
Cards tested:
Results:

