# Card Test Tracker

**Total Cards:** 281
**Untested:** 0
**Passed:** 281
**Failed:** 0

---

## Status Legend
- **Untested** - Not yet tested
- **Passed** - Tested and working correctly
- **Failed** - Tested and has issues (see notes)
- **Fixed** - Was failed, now fixed and passed

---

## All Cards

| ID | Name | Status | Notes |
|----|------|--------|-------|
| 0 | PerfectEarthBlessing | Passed | |
| 1 | GolemBlinker | Passed | Optional trigger from hand, bot auto-decline fix |
| 2 | GolemThrower | Passed | Choose effect with targeting |
| 3 | GolemTrampler | Passed | Trample, activated sacrifice |
| 4 | GoldGolem | Passed | |
| 5 | StoneSculptor | Passed | |
| 6 | Golem | Passed | |
| 7 | RockGolem | Passed | |
| 8 | GolemFounder | Passed | Reveal effect with amountBasedOn |
| 9 | AlchemyGolem | Passed | |
| 10 | IronGolems | Passed | |
| 11 | TransparentGolem | Passed | |
| 12 | StoneShaper | Passed | Choose effect with token creation |
| 13 | ReconfigureGolem | Passed | InZone condition trigger from graveyard |
| 14 | GolemSmasher | Passed | UI text duplication fix for granted passives |
| 15 | ReplacerGolem | Passed | Fixed leftZone trigger (card already moved when checking) |
| 16 | ExcavatorGolem | Passed | |
| 17 | Smash | Passed | X display, thisTurn buff, cancel fix |
| 18 | GolemBlesser | Passed | Clone/owner system for self-buff fix |
| 19 | MoltenGravel | Passed | Fixed conditional trample (GetKeywords now uses GetVerifiedPassives) |
| 20 | ChancellorGolem | Passed | Fixed opening hand trigger zone check |
| 21 | EarthquakeGolem | Passed | |
| 22 | LordGolem | Passed | Fixed aura (self: false in JSON) |
| 23 | RockToss | Passed | Conditional damage modifier fix (effectOwner vs affectedPlayer) |
| 24 | RockArms | Passed | Token buff fix |
| 25 | BrassGolem | Passed | Fixed dynamic token stats (passive with amountBasedOn), baseAttack/baseDefense for color |
| 26 | FoundryGolem | Passed | ModifyCost passive with stone condition |
| 27 | TreeOfBurningFire | Passed | GrantActive passive, token activation, self-sacrifice, deep copy Effect fix |
| 28 | BoneToPeaches | Passed | Fixed token targeting in GetPossibleTargets |
| 29 | Quarry | Passed | isPlayerTurn, description fix |
| 30 | DigUp | Passed | |
| 31 | Stones | Passed | |
| 32 | StoneToss | Passed | Variable X sacrifice, X-based life cost, 2X damage |
| 33 | Stoned | Passed | |
| 34 | MasterGolem | Passed | Tribute multiplier, StonesInPlay amountBasedOn |
| 35 | RockAvalanche | Passed | |
| 36 | DigitalStone | Passed | |
| 37 | StoneSearch | Passed | Fixed tutor-to-play event ordering (SendToZone before RefreshCardDisplays) |
| 38 | Foundry | Passed | |
| 39 | Woad-Hollow | Passed | Fixed mill trigger zone check |
| 40 | GolemGod | Passed | Implemented alternate sacrifice cost |
| 41 | StoneWall | Passed | |
| 42 | GamePlan | Passed | |
| 43 | MerfolkBalancer | Passed | |
| 44 | SeagateMerfolk | Passed | |
| 45 | Calculate | Passed | Counter spell targeting stack items |
| 46 | ChancellorMerfolk | Passed | |
| 47 | RiverSiren | Passed | |
| 48 | EidolonOfTheTides | Passed | |
| 49 | PoseidonsBlessing | Passed | |
| 50 | Fishies | Passed | |
| 51 | MerfolkGazer | Passed | |
| 52 | DiverMerfolk | Passed | |
| 53 | MerfolkRusher | Passed | |
| 54 | CuriousMerfolk | Passed | |
| 55 | MerfolkKeeper | Passed | Draw trigger with notFirst restriction, +1/+1 counters |
| 56 | MerfolkRevealer | Passed | Self-sacrifice, reveal target selection |
| 57 | MerfolkScrollkeeper | Passed | |
| 58 | TreeOfSafeguard | Passed | DisableKeyword aura, sacrifice self, reveal from hand |
| 59 | MerfolkGang | Passed | Cast from hand trigger, optional, cost restrictions |
| 60 | MerfolkLeader | Passed | |
| 61 | MerfolkFatekeeper | Passed | |
| 62 | MerfolkDeceiver | Passed | KeywordsOrAbilities restriction |
| 63 | Denial | Passed | |
| 64 | MerfolkShifter | Passed | |
| 65 | MerfolkFinder | Passed | |
| 66 | EadroMerfolkGod | Passed | DiscardOrSacrificeMerfolk cost, thisTurn passives, self-sacrifice, death trigger tokens |
| 67 | MerfolkScoper | Passed | |
| 68 | MerfolkSummoner | Passed | |
| 69 | SpreadingThornbush | Passed | |
| 70 | Dream | Passed | |
| 71 | MerfolkBase | Passed | |
| 72 | MerfolkSwarm | Passed | |
| 73 | MerfolkMaster | Passed | self:false aura fix for innate passives |
| 74 | Consider | Passed | |
| 75 | SiftRubble | Passed | |
| 76 | Snag | Passed | |
| 77 | BackSnap | Passed | Spell alternate cost ordering fix (cost choice before target selection) |
| 78 | DrawCounter | Passed | Counter target selection fix |
| 79 | Dispell | Passed | |
| 80 | MerfolkTribe | Passed | |
| 81 | Opt | Passed | Fixed infinite loop with resolve index, optional shuffle |
| 82 | Brainstorm | Passed | Multiplayer flow fix, shuffle message fix |
| 83 | CounterBalance | Passed | |
| 84 | Return | Passed | Each player effect |
| 85 | GodMerfolk | Passed | Fixed multiply stat description (x2/x2 shows "doubles") |
| 86 | Shatter | Passed | |
| 87 | Shell | Passed | Fixed CantBeTargeted passive description and targeting check |
| 88 | SnapShot | Passed | Fixed allOfSameName clone, token inclusion |
| 89 | TimeTwist | Passed | Fixed opponent hand visual sync, Spellburnt condition |
| 90 | CommandJustice | Passed | |
| 91 | Refresh | Passed | Implemented modifySummonLimit effect |
| 92 | WashAway | Passed | |
| 93 | TurnTime | Passed | Fixed phaseOfPlayer trigger, forOpponentChoice text |
| 94 | GodRecallSpell | Passed | |
| 95 | SwapControl | Passed | |
| 96 | DuskWraith | Passed | Conditional destroy/gainLife, target selection message fix |
| 97 | Ghastly | Passed | Opening hand playerChoice discard |
| 98 | GraveDigger | Passed | |
| 99 | LootGhost | Passed | |
| 100 | HaunterShade | Passed | Fixed JSON: triggeredEffects, trigger mill, self true, createToken |
| 101 | WitnessShade | Passed | Fixed each player mill (two effects with isOpponent), description override |
| 102 | GhostGathering | Passed | Replacement effect (summons to exile), playerChoice castCard, mill trigger fix |
| 103 | ShadeHerald | Passed | |
| 104 | ShadowDancer | Passed | |
| 105 | SelflessShadow | Passed | Tribute trigger, token with keywords |
| 106 | ShadowOfTheGrave | Passed | Innate passive scope fix |
| 107 | GhastlyTutor | Passed | |
| 108 | ShadeCrawler | Passed | Graveyard trigger, targetBasedOn triggerCard |
| 109 | GhostReceiver | Passed | Mill upTo amount selection fix |
| 110 | ShadeRunner | Passed | |
| 111 | GhostDeceiver | Passed | |
| 112 | DarkBlessing | Passed | |
| 113 | ShadeOfReturn | Passed | |
| 114 | BluntAmbusher | Passed | Life-dependent passive stats refresh |
| 115 | DoubleShadow | Passed | |
| 116 | RelentingShade | Passed | Trigger scope fix, resolution-time inZone condition |
| 117 | ThreeShadows | Passed | |
| 118 | Shade | Passed | |
| 119 | HandRefresh | Passed | |
| 120 | Reap | Passed | Opponent discard selection, player UID preservation fix |
| 121 | LingeringShades | Passed | |
| 122 | LostButNeverGone | Passed | |
| 123 | GhostlyLooter | Passed | |
| 124 | DarkShade | Passed | Discard trigger skip card qualification, bot auto-discard, oncePerTurn tracking |
| 125 | Fisher | Passed | targetType opponent fix, weakest summon targeting |
| 126 | Vanquish | Passed | |
| 127 | ShadowLord | Passed | Graveyard aura fix, statModifiers typo, passive removal on zone change |
| 128 | Edict | Passed | eachPlayer sacrifice handling |
| 129 | Fable | Passed | |
| 130 | ItsAlive | Passed | |
| 131 | Reaper | Passed | resolveTarget discard, Summon restriction, reveal filtering |
| 132 | Strongfall | Passed | targetType opponent, strongest summon targeting |
| 133 | Duress | Passed | resolveTarget castability fix, reveal only matching cards |
| 134 | HauntGod | Passed | Tribute altCostType handling in CanPayAlternateCost, RequestActivatedAbilityAltCostPayment, HandleCostSelection |
| 135 | ExchangeSouls | Passed | resolveTarget for mill-then-select, targetType cardInGraveyard |
| 136 | Wrath | Passed | Implemented destroy all effect |
| 137 | CrawlBack | Passed | Fixed targetType to cardInGraveyard |
| 138 | ChainofBolts | Passed | opponentsChoice for resolve-time target selection by opponent |
| 139 | Gobby | Passed | Draw effect description for targetType opponent |
| 140 | LootingFire | Passed | resolveTarget for conditional targeting, Control condition default minAmount=1 |
| 141 | GobLaunch | Passed | AdditionalCost tribe/cardType check, sacrifice only from play, TargetAttack condition |
| 142 | ExploderGob | Passed | affectedPlayer for non-targeted damage, DealDamage effect description |
| 143 | SpearGob | Passed | |
| 144 | BlitzGoblin | Passed | |
| 145 | GobRocket | Passed | |
| 146 | TransparentGoblin | Passed | |
| 147 | GoblinDuelist | Passed | attackedSummon trigger, survivedCombat sacrifice |
| 148 | Maglubiyet'sBlessing | Passed | |
| 149 | GobRunner | Passed | |
| 150 | FuryGoblin | Passed | |
| 151 | GoblinCrew | Passed | |
| 152 | LooterGob | Passed | Cast trigger, isCost reveal selection |
| 153 | UndeadGoblin | Passed | ImmuneToKeyword (Dive, Trample, Haunt) |
| 154 | GoblinRitualist | Passed | CastCard from graveyard with select, free cast implementation |
| 155 | Shot | Passed | modifierConditions with control condition |
| 156 | FireMasterGob | Passed | Cast trigger with spellburnt conditions, tribe filter |
| 157 | FiringGoblin | Passed | isCost reveal with resolveTarget damage |
| 158 | GoblinSquadron | Passed | Token with tributeRestriction passive |
| 159 | BabyGobs | Passed | Token with tributeRestriction passive |
| 160 | GoblinEngineer | Passed | Tutor with reveal |
| 161 | GoblinMomma | Passed | Dual token creation |
| 162 | GoblinPortal | Passed | Opening hand trigger, tribute with keyword filter |
| 163 | GoblinMaster | Passed | goblinsInPlay amountBasedOn passive |
| 164 | GoblinRally | Passed | Attack trigger, tokenAttacking |
| 165 | GoblinTrickster | Passed | Spell tutor with reveal |
| 166 | GoblinGod | Passed | bypassSummonLimit, goblinsControlled amountBasedOn |
| 167 | RallyTheMogs | Passed | Aura passives: changeStats + grantKeyword |
| 168 | ForkBolt | Passed | |
| 169 | ChieftanGob | Passed | |
| 170 | Greed | Passed | Control condition with maxAmount:0 fix |
| 171 | HeatRay | Passed | |
| 172 | GoblinTown | Passed | CreateToken amountModifier display fix |
| 173 | Smite | Passed | |
| 174 | ExplosiveVegetation | Passed | TributeMultiplier for summons, ModifyType rootEffect.affectedUids filtering |
| 175 | Gamble | Passed | |
| 176 | RunAmok | Passed | CantTribute passive implementation |
| 177 | HeatWave | Passed | |
| 178 | Fireblast | Passed | Damage modifier with goblin control, alternate exile cost |
| 179 | Obliterate | Passed | Damage to both players, cantGainLife for both |
| 180 | Wildfire | Passed | DynamicAdd cost modifier, SummonsOpponentControls |
| 181 | Channel | Passed | |
| 182 | PlantOfSolitude | Passed | CantTribute passive, tribute trigger scope:all, opponent choice deep clone fix |
| 183 | EternalTreefolk | Passed | Optional at trigger level, HasInZone condition |
| 184 | JeelaiPlant | Passed | |
| 185 | CipplingVines | Passed | NonTreefolk restriction, TreefolkControlled amountBasedOn |
| 186 | PlantOfHerbs | Passed | |
| 187 | NaturesBlessing | Passed | |
| 188 | TreeSavant | Passed | |
| 189 | GrappleRoots | Passed | |
| 190 | SproutPlant | Passed | |
| 191 | VinePlant | Passed | |
| 192 | Treefice | Passed | |
| 193 | GiverOfPlants | Passed | |
| 194 | SproutAnArmy | Passed | Herb sacrifice selection, cantGainLife choice text |
| 195 | Planter | Passed | |
| 196 | TallTreefolk | Passed | |
| 197 | NaturalStatePlant | Passed | Graveyard ability selection fix, inspection panel autoPass fix |
| 198 | PlantGrower | Passed | Phase trigger with attacked condition, scope selfOnly sacrifice |
| 199 | Sproutlings | Passed | |
| 200 | PlantSprouter | Passed | Cast trigger, TreefolkControlled fix (exclude tokens) |
| 201 | PlatePlant | Passed | |
| 202 | CliffsideSprout | Passed | phaseOfPlayer fix for controller-only draw trigger |
| 203 | GlowingSpore | Passed | RootController fix for destroyed cards using lastControllingPlayer |
| 204 | DeadTree | Passed | zone vs targetZones fix for non-targeting sendToZone |
| 205 | GiftOfNature | Passed | targetType graveyard for target selection |
| 206 | TransparentPlant | Passed | activateFromHand ability, targetType/tribe on inner effect |
| 207 | PerfectBog | Passed | |
| 208 | TreeGiant | Passed | DefenseUsedForAttack passive, activateFromHand discard ability |
| 209 | SpiritTree | Passed | |
| 210 | WarbriarStomper | Passed | |
| 211 | VerdictCommand | Passed | RemoveCounter effect for haunt counters |
| 212 | Entangle | Passed | conditionMax/cardType fix for Control condition |
| 213 | Harvest | Passed | |
| 214 | LostSanctuary | Passed | Multiple choose effects, opponent choice handling |
| 215 | PlanterBox | Passed | thisTurn token death |
| 216 | GrowTall | Passed | IsTribe condition, targetBasedOn rootAffected |
| 217 | Fertilize | Passed | Player passives for futureProof keyword grants |
| 218 | PlantofTrees | Passed | |
| 219 | SproutUp | Passed | |
| 220 | Overrun | Passed | |
| 221 | Uncover | Passed | |
| 222 | Simplify | Passed | |
| 223 | TreeGod | Passed | |
| 224 | Green-Sun | Passed | |
| 225 | Grow | Passed | |
| 226 | PlantSnap | Passed | |
| 227 | Gigatrunk | Passed | |
| 228 | DeeprootGuard | Passed | |
| 229 | MasterTree | Passed | CantAttack passive |
| 230 | TreeOfLife | Passed | Self-sacrifice, targetType cardInHand |
| 231 | Herblore | Passed | BypassHerbLifeReduction player passive |
| 232 | Barrage | Passed | |
| 233 | FlameWave | Passed | |
| 234 | RitualOfDarkness | Passed | CardRitualOfDarkness effect, SendToZone for Hand->Play, trigger deferral |
| 235 | Spectralize | Passed | ExileAndReturn with targeting, targetBasedOn rootAffected |
| 236 | DreamBig | Passed | |
| 237 | AvalancheGolem | Passed | Variable stone sacrifice, playerChosenAmount, all damage |
| 238 | CastAMold | Passed | |
| 239 | GroundTactics | Passed | |
| 240 | BreakThrough | Passed | Attacking restriction, granted keywords, additionalEffects targetBasedOn |
| 241 | FoundationGolem | Passed | phaseOfPlayer for controller-only trigger |
| 242 | GraniteGolem | Passed | |
| 243 | TargetDummy | Passed | Taunt keyword implementation |
| 244 | ShatteringSmash | Passed | requireOneFromEach targeting, BothPlayersHaveSummons cast restriction |
| 245 | PutridFolks | Passed | ExileAndReturn with SendToZone |
| 246 | Riptide | Passed | Token destruction includes Token class instances |
| 247 | MerfolkFateseer | Passed | |
| 248 | BeachedMerfolk | Passed | |
| 249 | Legionaires | Passed | |
| 250 | SlipspaceMerfolk | Passed | Auto-sacrifice, targetType permanent |
| 251 | MerfolkElite | Passed | |
| 252 | MerfolkMage | Passed | Copy spell trigger, TriggeredEffect type, rootEffect clone fix |
| 253 | SkyScryerMerfolk | Passed | TopCardRevealed passive, deck top card UI, click-to-cast |
| 254 | Typhoon | Passed | |
| 255 | Rewind | Passed | GoToPhase event for turn restart |
| 256 | SpawnFish | Passed | |
| 257 | GeistOfDroolingTears | Passed | |
| 258 | RecurringNightmare | Passed | isPlayerTurn for draw phase trigger |
| 259 | SetStraight | Passed | SourcePlayer targetBasedOn, HalfLife amountBasedOn, any number selection |
| 260 | Spectral Amulet | Passed | Object type card handling (no attack, no tribute) |
| 261 | RestlessGhost | Passed | |
| 262 | BurstLightning | Passed | selectRepeatUpfront, upfront life cost, skip invalid targets |
| 263 | GoblinGrunt | Passed | |
| 264 | CavalcadePyromancer | Passed | Cast trigger cardType filter, TriggerController targetBasedOn |
| 265 | Ringleader Champion | Passed | Grant passive option text fix |
| 266 | SearingFire | Passed | |
| 267 | GoblinChanneler | Passed | Cost restriction for triggers, TriggerController targetBasedOn |
| 268 | SearingGoblin | Passed | DisableEnterPlayEffects passive |
| 269 | GoblinLieutenant | Passed | |
| 270 | BlastOpen | Passed | DefenseGreaterThanAttack restriction |
| 271 | GoblinTactician | Passed | FinalAttack damage, proper targeting, option message |
| 272 | AllOutAttackCommander | Passed | CantSpecialSummon with selfOnly scope |
| 273 | UndyingDeathwood | Passed | InZones condition, freeCast, TriggerSprout, scope all for mill |
| 274 | EndlessGarden | Passed | eventTriggers for delayed phase trigger on stack |
| 275 | UnendingSundew | Passed | immediate ability, eventTriggers for delayed return |
| 276 | FromDust | Passed | targetType for graveyard |
| 277 | PottedFlower | Passed | |
| 278 | TreeOfAbundance | Passed | TokenCanTribute, CreateTokenModifier (*2), token stacking fix |
| 279 | GroundControl | Passed | |
| 280 | BloomingMarsh | Passed | DidntAttack condition, phase trigger |

---

## Test Session Log

### Session 1 - [Date]
Cards tested:
Results:

