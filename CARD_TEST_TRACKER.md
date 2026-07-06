# Card Test Tracker

**Total Cards:** 281
**Untested:** 252
**Passed:** 21
**Failed:** 8

---

## Status Legend
- **Untested** - Not yet tested
- **Passed** - Tested and working correctly
- **Failed** - Tested and has issues (see notes)
- **Fixed** - Was failed, now fixed and passed

---

## All Cards

| ID | Name | Status | Round 1 Notes | Round 2 Notes |
|----|------|--------|---------------|---------------|
| 0 | PerfectEarthBlessing | Passed |  |  |
| 1 | GolemBlinker | Passed | Optional trigger from hand, bot auto-decline fix |  |
| 2 | GolemThrower | Passed | Choose effect with targeting |  |
| 3 | GolemTrampler | Failed | Trample, activated sacrifice |  |
| 4 | GoldGolem | Passed |  |  |
| 5 | StoneSculptor | Passed |  |  |
| 6 | Golem | Passed |  |  |
| 7 | RockGolem | Passed |  |  |
| 8 | GolemFounder | Passed | Reveal effect with amountBasedOn |  |
| 9 | AlchemyGolem | Passed |  |  |
| 10 | IronGolems | Passed |  |  |
| 11 | TransparentGolem | Passed |  |  |
| 12 | StoneShaper | Passed | Choose effect with token creation |  |
| 13 | ReconfigureGolem | Failed | InZone condition trigger from graveyard |  |
| 14 | GolemSmasher | Failed | UI text duplication fix for granted passives |  |
| 15 | ReplacerGolem | Passed | Fixed leftZone trigger (card already moved when checking) |  |
| 16 | ExcavatorGolem | Passed |  |  |
| 17 | Smash | Passed | X display, thisTurn buff, cancel fix |  |
| 18 | GolemBlesser | Failed | Clone/owner system for self-buff fix |  |
| 19 | MoltenGravel | Untested | Fixed conditional trample (GetKeywords now uses GetVerifiedPassives) |  |
| 20 | ChancellorGolem | Passed | Fixed opening hand trigger zone check |  |
| 21 | EarthquakeGolem | Passed |  |  |
| 22 | LordGolem | Untested | Fixed aura (self: false in JSON) |  |
| 23 | RockToss | Failed | Conditional damage modifier fix (effectOwner vs affectedPlayer) |  |
| 24 | RockArms | Passed | Token buff fix |  |
| 25 | BrassGolem | Passed | Fixed dynamic token stats (passive with amountBasedOn), baseAttack/baseDefense for color |  |
| 26 | FoundryGolem | Failed | ModifyCost passive with stone condition |  |
| 27 | TreeOfBurningFire | Untested | GrantActive passive, token activation, self-sacrifice, deep copy Effect fix |  |
| 28 | BoneToPeaches | Untested | Fixed token targeting in GetPossibleTargets |  |
| 29 | Quarry | Failed | isPlayerTurn, description fix |  |
| 30 | DigUp | Passed |  |  |
| 31 | Stones | Untested |  |  |
| 32 | StoneToss | Passed | Variable X sacrifice, X-based life cost, 2X damage |  |
| 33 | Stoned | Untested |  |  |
| 34 | MasterGolem | Untested | Tribute multiplier, StonesInPlay amountBasedOn |  |
| 35 | RockAvalanche | Failed |  |  |
| 36 | DigitalStone | Untested |  |  |
| 37 | StoneSearch | Untested | Fixed tutor-to-play event ordering (SendToZone before RefreshCardDisplays) |  |
| 38 | Foundry | Untested |  |  |
| 39 | Woad-Hollow | Untested | Fixed mill trigger zone check |  |
| 40 | GolemGod | Untested | Implemented alternate sacrifice cost |  |
| 41 | StoneWall | Untested |  |  |
| 42 | GamePlan | Untested |  |  |
| 43 | MerfolkBalancer | Untested |  |  |
| 44 | SeagateMerfolk | Untested |  |  |
| 45 | Calculate | Untested | Counter spell targeting stack items |  |
| 46 | ChancellorMerfolk | Untested |  |  |
| 47 | RiverSiren | Untested |  |  |
| 48 | EidolonOfTheTides | Untested |  |  |
| 49 | PoseidonsBlessing | Untested |  |  |
| 50 | Fishies | Untested |  |  |
| 51 | MerfolkGazer | Untested |  |  |
| 52 | DiverMerfolk | Untested |  |  |
| 53 | MerfolkRusher | Untested |  |  |
| 54 | CuriousMerfolk | Untested |  |  |
| 55 | MerfolkKeeper | Untested | Draw trigger with notFirst restriction, +1/+1 counters |  |
| 56 | MerfolkRevealer | Untested | Self-sacrifice, reveal target selection |  |
| 57 | MerfolkScrollkeeper | Untested |  |  |
| 58 | TreeOfSafeguard | Untested | DisableKeyword aura, sacrifice self, reveal from hand |  |
| 59 | MerfolkGang | Untested | Cast from hand trigger, optional, cost restrictions |  |
| 60 | MerfolkLeader | Untested |  |  |
| 61 | MerfolkFatekeeper | Untested |  |  |
| 62 | MerfolkDeceiver | Untested | KeywordsOrAbilities restriction |  |
| 63 | Denial | Untested |  |  |
| 64 | MerfolkShifter | Untested |  |  |
| 65 | MerfolkFinder | Untested |  |  |
| 66 | EadroMerfolkGod | Untested | DiscardOrSacrificeMerfolk cost, thisTurn passives, self-sacrifice, death trigger tokens |  |
| 67 | MerfolkScoper | Untested |  |  |
| 68 | MerfolkSummoner | Untested |  |  |
| 69 | SpreadingThornbush | Untested |  |  |
| 70 | Dream | Untested |  |  |
| 71 | MerfolkBase | Untested |  |  |
| 72 | MerfolkSwarm | Untested |  |  |
| 73 | MerfolkMaster | Untested | self:false aura fix for innate passives |  |
| 74 | Consider | Untested |  |  |
| 75 | SiftRubble | Untested |  |  |
| 76 | Snag | Untested |  |  |
| 77 | BackSnap | Untested | Spell alternate cost ordering fix (cost choice before target selection) |  |
| 78 | DrawCounter | Untested | Counter target selection fix |  |
| 79 | Dispell | Untested |  |  |
| 80 | MerfolkTribe | Untested |  |  |
| 81 | Opt | Untested | Fixed infinite loop with resolve index, optional shuffle |  |
| 82 | Brainstorm | Untested | Multiplayer flow fix, shuffle message fix |  |
| 83 | CounterBalance | Untested |  |  |
| 84 | Return | Untested | Each player effect |  |
| 85 | GodMerfolk | Untested | Fixed multiply stat description (x2/x2 shows "doubles") |  |
| 86 | Shatter | Untested |  |  |
| 87 | Shell | Untested | Fixed CantBeTargeted passive description and targeting check |  |
| 88 | SnapShot | Untested | Fixed allOfSameName clone, token inclusion |  |
| 89 | TimeTwist | Untested | Fixed opponent hand visual sync, Spellburnt condition |  |
| 90 | CommandJustice | Untested |  |  |
| 91 | Refresh | Untested | Implemented modifySummonLimit effect |  |
| 92 | WashAway | Untested |  |  |
| 93 | TurnTime | Untested | Fixed phaseOfPlayer trigger, forOpponentChoice text |  |
| 94 | GodRecallSpell | Untested |  |  |
| 95 | SwapControl | Untested |  |  |
| 96 | DuskWraith | Untested | Conditional destroy/gainLife, target selection message fix |  |
| 97 | Ghastly | Untested | Opening hand playerChoice discard |  |
| 98 | GraveDigger | Untested |  |  |
| 99 | LootGhost | Untested |  |  |
| 100 | HaunterShade | Untested | Fixed JSON: triggeredEffects, trigger mill, self true, createToken |  |
| 101 | WitnessShade | Untested | Fixed each player mill (two effects with isOpponent), description override |  |
| 102 | GhostGathering | Untested | Replacement effect (summons to exile), playerChoice castCard, mill trigger fix |  |
| 103 | ShadeHerald | Untested |  |  |
| 104 | ShadowDancer | Untested |  |  |
| 105 | SelflessShadow | Untested | Tribute trigger, token with keywords |  |
| 106 | ShadowOfTheGrave | Untested | Innate passive scope fix |  |
| 107 | GhastlyTutor | Untested |  |  |
| 108 | ShadeCrawler | Untested | Graveyard trigger, targetBasedOn triggerCard |  |
| 109 | GhostReceiver | Untested | Mill upTo amount selection fix |  |
| 110 | ShadeRunner | Untested |  |  |
| 111 | GhostDeceiver | Untested |  |  |
| 112 | DarkBlessing | Untested |  |  |
| 113 | ShadeOfReturn | Untested |  |  |
| 114 | BluntAmbusher | Untested | Life-dependent passive stats refresh |  |
| 115 | DoubleShadow | Untested |  |  |
| 116 | RelentingShade | Untested | Trigger scope fix, resolution-time inZone condition |  |
| 117 | ThreeShadows | Untested |  |  |
| 118 | Shade | Untested |  |  |
| 119 | HandRefresh | Untested |  |  |
| 120 | Reap | Untested | Opponent discard selection, player UID preservation fix |  |
| 121 | LingeringShades | Untested |  |  |
| 122 | LostButNeverGone | Untested |  |  |
| 123 | GhostlyLooter | Untested |  |  |
| 124 | DarkShade | Untested | Discard trigger skip card qualification, bot auto-discard, oncePerTurn tracking |  |
| 125 | Fisher | Untested | targetType opponent fix, weakest summon targeting |  |
| 126 | Vanquish | Untested |  |  |
| 127 | ShadowLord | Untested | Graveyard aura fix, statModifiers typo, passive removal on zone change |  |
| 128 | Edict | Untested | eachPlayer sacrifice handling |  |
| 129 | Fable | Untested |  |  |
| 130 | ItsAlive | Untested |  |  |
| 131 | Reaper | Untested | resolveTarget discard, Summon restriction, reveal filtering |  |
| 132 | Strongfall | Untested | targetType opponent, strongest summon targeting |  |
| 133 | Duress | Untested | resolveTarget castability fix, reveal only matching cards |  |
| 134 | HauntGod | Untested | Tribute altCostType handling in CanPayAlternateCost, RequestActivatedAbilityAltCostPayment, HandleCostSelection |  |
| 135 | ExchangeSouls | Untested | resolveTarget for mill-then-select, targetType cardInGraveyard |  |
| 136 | Wrath | Untested | Implemented destroy all effect |  |
| 137 | CrawlBack | Untested | Fixed targetType to cardInGraveyard |  |
| 138 | ChainofBolts | Untested | opponentsChoice for resolve-time target selection by opponent |  |
| 139 | Gobby | Untested | Draw effect description for targetType opponent |  |
| 140 | LootingFire | Untested | resolveTarget for conditional targeting, Control condition default minAmount=1 |  |
| 141 | GobLaunch | Untested | AdditionalCost tribe/cardType check, sacrifice only from play, TargetAttack condition |  |
| 142 | ExploderGob | Untested | affectedPlayer for non-targeted damage, DealDamage effect description |  |
| 143 | SpearGob | Untested |  |  |
| 144 | BlitzGoblin | Untested |  |  |
| 145 | GobRocket | Untested |  |  |
| 146 | TransparentGoblin | Untested |  |  |
| 147 | GoblinDuelist | Untested | attackedSummon trigger, survivedCombat sacrifice |  |
| 148 | Maglubiyet'sBlessing | Untested |  |  |
| 149 | GobRunner | Untested |  |  |
| 150 | FuryGoblin | Untested |  |  |
| 151 | GoblinCrew | Untested |  |  |
| 152 | LooterGob | Untested | Cast trigger, isCost reveal selection |  |
| 153 | UndeadGoblin | Untested | ImmuneToKeyword (Dive, Trample, Haunt) |  |
| 154 | GoblinRitualist | Untested | CastCard from graveyard with select, free cast implementation |  |
| 155 | Shot | Untested | modifierConditions with control condition |  |
| 156 | FireMasterGob | Untested | Cast trigger with spellburnt conditions, tribe filter |  |
| 157 | FiringGoblin | Untested | isCost reveal with resolveTarget damage |  |
| 158 | GoblinSquadron | Untested | Token with tributeRestriction passive |  |
| 159 | BabyGobs | Untested | Token with tributeRestriction passive |  |
| 160 | GoblinEngineer | Untested | Tutor with reveal |  |
| 161 | GoblinMomma | Untested | Dual token creation |  |
| 162 | GoblinPortal | Untested | Opening hand trigger, tribute with keyword filter |  |
| 163 | GoblinMaster | Untested | goblinsInPlay amountBasedOn passive |  |
| 164 | GoblinRally | Untested | Attack trigger, tokenAttacking |  |
| 165 | GoblinTrickster | Untested | Spell tutor with reveal |  |
| 166 | GoblinGod | Untested | bypassSummonLimit, goblinsControlled amountBasedOn |  |
| 167 | RallyTheMogs | Untested | Aura passives: changeStats + grantKeyword |  |
| 168 | ForkBolt | Untested |  |  |
| 169 | ChieftanGob | Untested |  |  |
| 170 | Greed | Untested | Control condition with maxAmount:0 fix |  |
| 171 | HeatRay | Untested |  |  |
| 172 | GoblinTown | Untested | CreateToken amountModifier display fix |  |
| 173 | Smite | Untested |  |  |
| 174 | ExplosiveVegetation | Untested | TributeMultiplier for summons, ModifyType rootEffect.affectedUids filtering |  |
| 175 | Gamble | Untested |  |  |
| 176 | RunAmok | Untested | CantTribute passive implementation |  |
| 177 | HeatWave | Untested |  |  |
| 178 | Fireblast | Untested | Damage modifier with goblin control, alternate exile cost |  |
| 179 | Obliterate | Untested | Damage to both players, cantGainLife for both |  |
| 180 | Wildfire | Untested | DynamicAdd cost modifier, SummonsOpponentControls |  |
| 181 | Channel | Untested |  |  |
| 182 | PlantOfSolitude | Untested | CantTribute passive, tribute trigger scope:all, opponent choice deep clone fix |  |
| 183 | EternalTreefolk | Untested | Optional at trigger level, HasInZone condition |  |
| 184 | JeelaiPlant | Untested |  |  |
| 185 | CipplingVines | Untested | NonTreefolk restriction, TreefolkControlled amountBasedOn |  |
| 186 | PlantOfHerbs | Untested |  |  |
| 187 | NaturesBlessing | Untested |  |  |
| 188 | TreeSavant | Untested |  |  |
| 189 | GrappleRoots | Untested |  |  |
| 190 | SproutPlant | Untested |  |  |
| 191 | VinePlant | Untested |  |  |
| 192 | Treefice | Untested |  |  |
| 193 | GiverOfPlants | Untested |  |  |
| 194 | SproutAnArmy | Untested | Herb sacrifice selection, cantGainLife choice text |  |
| 195 | Planter | Untested |  |  |
| 196 | TallTreefolk | Untested |  |  |
| 197 | NaturalStatePlant | Untested | Graveyard ability selection fix, inspection panel autoPass fix |  |
| 198 | PlantGrower | Untested | Phase trigger with attacked condition, scope selfOnly sacrifice |  |
| 199 | Sproutlings | Untested |  |  |
| 200 | PlantSprouter | Untested | Cast trigger, TreefolkControlled fix (exclude tokens) |  |
| 201 | PlatePlant | Untested |  |  |
| 202 | CliffsideSprout | Untested | phaseOfPlayer fix for controller-only draw trigger |  |
| 203 | GlowingSpore | Untested | RootController fix for destroyed cards using lastControllingPlayer |  |
| 204 | DeadTree | Untested | zone vs targetZones fix for non-targeting sendToZone |  |
| 205 | GiftOfNature | Untested | targetType graveyard for target selection |  |
| 206 | TransparentPlant | Untested | activateFromHand ability, targetType/tribe on inner effect |  |
| 207 | PerfectBog | Untested |  |  |
| 208 | TreeGiant | Untested | DefenseUsedForAttack passive, activateFromHand discard ability |  |
| 209 | SpiritTree | Untested |  |  |
| 210 | WarbriarStomper | Untested |  |  |
| 211 | VerdictCommand | Untested | RemoveCounter effect for haunt counters |  |
| 212 | Entangle | Untested | conditionMax/cardType fix for Control condition |  |
| 213 | Harvest | Untested |  |  |
| 214 | LostSanctuary | Untested | Multiple choose effects, opponent choice handling |  |
| 215 | PlanterBox | Untested | thisTurn token death |  |
| 216 | GrowTall | Untested | IsTribe condition, targetBasedOn rootAffected |  |
| 217 | Fertilize | Untested | Player passives for futureProof keyword grants |  |
| 218 | PlantofTrees | Untested |  |  |
| 219 | SproutUp | Untested |  |  |
| 220 | Overrun | Untested |  |  |
| 221 | Uncover | Untested |  |  |
| 222 | Simplify | Untested |  |  |
| 223 | TreeGod | Untested |  |  |
| 224 | Green-Sun | Untested |  |  |
| 225 | Grow | Untested |  |  |
| 226 | PlantSnap | Untested |  |  |
| 227 | Gigatrunk | Untested |  |  |
| 228 | DeeprootGuard | Untested |  |  |
| 229 | MasterTree | Untested | CantAttack passive |  |
| 230 | TreeOfLife | Untested | Self-sacrifice, targetType cardInHand |  |
| 231 | Herblore | Untested | BypassHerbLifeReduction player passive |  |
| 232 | Barrage | Untested |  |  |
| 233 | FlameWave | Untested |  |  |
| 234 | RitualOfDarkness | Untested | CardRitualOfDarkness effect, SendToZone for Hand->Play, trigger deferral |  |
| 235 | Spectralize | Untested | ExileAndReturn with targeting, targetBasedOn rootAffected |  |
| 236 | DreamBig | Untested |  |  |
| 237 | AvalancheGolem | Untested | Variable stone sacrifice, playerChosenAmount, all damage |  |
| 238 | CastAMold | Untested |  |  |
| 239 | GroundTactics | Untested |  |  |
| 240 | BreakThrough | Untested | Attacking restriction, granted keywords, additionalEffects targetBasedOn |  |
| 241 | FoundationGolem | Untested | phaseOfPlayer for controller-only trigger |  |
| 242 | GraniteGolem | Untested |  |  |
| 243 | TargetDummy | Untested | Taunt keyword implementation |  |
| 244 | ShatteringSmash | Untested | requireOneFromEach targeting, BothPlayersHaveSummons cast restriction |  |
| 245 | PutridFolks | Untested | ExileAndReturn with SendToZone |  |
| 246 | Riptide | Untested | Token destruction includes Token class instances |  |
| 247 | MerfolkFateseer | Untested |  |  |
| 248 | BeachedMerfolk | Untested |  |  |
| 249 | Legionaires | Untested |  |  |
| 250 | SlipspaceMerfolk | Untested | Auto-sacrifice, targetType permanent |  |
| 251 | MerfolkElite | Untested |  |  |
| 252 | MerfolkMage | Untested | Copy spell trigger, TriggeredEffect type, rootEffect clone fix |  |
| 253 | SkyScryerMerfolk | Untested | TopCardRevealed passive, deck top card UI, click-to-cast |  |
| 254 | Typhoon | Untested |  |  |
| 255 | Rewind | Untested | GoToPhase event for turn restart |  |
| 256 | SpawnFish | Untested |  |  |
| 257 | GeistOfDroolingTears | Untested |  |  |
| 258 | RecurringNightmare | Untested | isPlayerTurn for draw phase trigger |  |
| 259 | SetStraight | Untested | SourcePlayer targetBasedOn, HalfLife amountBasedOn, any number selection |  |
| 260 | Spectral Amulet | Untested | Object type card handling (no attack, no tribute) |  |
| 261 | RestlessGhost | Untested |  |  |
| 262 | BurstLightning | Untested | selectRepeatUpfront, upfront life cost, skip invalid targets |  |
| 263 | GoblinGrunt | Untested |  |  |
| 264 | CavalcadePyromancer | Untested | Cast trigger cardType filter, TriggerController targetBasedOn |  |
| 265 | Ringleader Champion | Untested | Grant passive option text fix |  |
| 266 | SearingFire | Untested |  |  |
| 267 | GoblinChanneler | Untested | Cost restriction for triggers, TriggerController targetBasedOn |  |
| 268 | SearingGoblin | Untested | DisableEnterPlayEffects passive |  |
| 269 | GoblinLieutenant | Untested |  |  |
| 270 | BlastOpen | Untested | DefenseGreaterThanAttack restriction |  |
| 271 | GoblinTactician | Untested | FinalAttack damage, proper targeting, option message |  |
| 272 | AllOutAttackCommander | Untested | CantSpecialSummon with selfOnly scope |  |
| 273 | UndyingDeathwood | Untested | InZones condition, freeCast, TriggerSprout, scope all for mill |  |
| 274 | EndlessGarden | Untested | eventTriggers for delayed phase trigger on stack |  |
| 275 | UnendingSundew | Untested | immediate ability, eventTriggers for delayed return |  |
| 276 | FromDust | Untested | targetType for graveyard |  |
| 277 | PottedFlower | Untested |  |  |
| 278 | TreeOfAbundance | Untested | TokenCanTribute, CreateTokenModifier (*2), token stacking fix |  |
| 279 | GroundControl | Untested |  |  |
| 280 | BloomingMarsh | Untested | DidntAttack condition, phase trigger |  |

---

## Test Session Log

### Session 1 - [Date]
Cards tested:
Results:

