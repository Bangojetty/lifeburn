# Card Test Tracker

**Total Cards:** 281
**Untested:** 175
**Passed:** 106
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
| 98 | GraveDigger | Untested | |
| 99 | LootGhost | Passed | |
| 100 | HaunterShade | Passed | Fixed JSON: triggeredEffects, trigger mill, self true, createToken |
| 101 | WitnessShade | Passed | Fixed each player mill (two effects with isOpponent), description override |
| 102 | GhostGathering | Passed | Replacement effect (summons to exile), playerChoice castCard, mill trigger fix |
| 103 | ShadeHerald | Passed | |
| 104 | ShadowDancer | Passed | |
| 105 | SelflessShadow | Untested | |
| 106 | ShadowOfTheGrave | Untested | |
| 107 | GhastlyTutor | Untested | |
| 108 | ShadeCrawler | Untested | |
| 109 | GhostReceiver | Untested | |
| 110 | ShadeRunner | Untested | |
| 111 | GhostDeceiver | Untested | |
| 112 | DarkBlessing | Untested | |
| 113 | ShadeOfReturn | Untested | |
| 114 | BluntAmbusher | Untested | |
| 115 | DoubleShadow | Untested | |
| 116 | RelentingShade | Untested | |
| 117 | ThreeShadows | Untested | |
| 118 | Shade | Untested | |
| 119 | HandRefresh | Untested | |
| 120 | Reap | Untested | |
| 121 | LingeringShades | Untested | |
| 122 | LostButNeverGone | Untested | |
| 123 | GhostlyLooter | Untested | |
| 124 | DarkShade | Untested | |
| 125 | Fisher | Untested | |
| 126 | Vanquish | Untested | |
| 127 | ShadowLord | Untested | |
| 128 | Edict | Untested | |
| 129 | Fable | Untested | |
| 130 | ItsAlive | Untested | |
| 131 | Reaper | Untested | |
| 132 | Strongfall | Untested | |
| 133 | Duress | Untested | |
| 134 | HauntGod | Untested | |
| 135 | ExchangeSouls | Untested | |
| 136 | Wrath | Untested | |
| 137 | CrawlBack | Untested | |
| 138 | ChainofBolts | Untested | |
| 139 | Gobby | Untested | |
| 140 | LootingFire | Untested | |
| 141 | GobLaunch | Untested | |
| 142 | ExploderGob | Untested | |
| 143 | SpearGob | Untested | |
| 144 | BlitzGoblin | Untested | |
| 145 | GobRocket | Untested | |
| 146 | TransparentGoblin | Untested | |
| 147 | GoblinDuelist | Untested | |
| 148 | Maglubiyet'sBlessing | Untested | |
| 149 | GobRunner | Untested | |
| 150 | FuryGoblin | Untested | |
| 151 | GoblinCrew | Untested | |
| 152 | LooterGob | Untested | |
| 153 | UndeadGoblin | Untested | |
| 154 | GoblinRitualist | Untested | |
| 155 | Shot | Untested | |
| 156 | FireMasterGob | Untested | |
| 157 | FiringGoblin | Untested | |
| 158 | GoblinSquadron | Untested | |
| 159 | BabyGobs | Untested | |
| 160 | GoblinEngineer | Untested | |
| 161 | GoblinMomma | Untested | |
| 162 | GoblinPortal | Untested | |
| 163 | GoblinMaster | Untested | |
| 164 | GoblinRally | Untested | |
| 165 | GoblinTrickster | Untested | |
| 166 | GoblinGod | Untested | |
| 167 | RallyTheMogs | Untested | |
| 168 | ForkBolt | Untested | |
| 169 | ChieftanGob | Untested | |
| 170 | Greed | Untested | |
| 171 | HeatRay | Untested | |
| 172 | GoblinTown | Untested | |
| 173 | Smite | Untested | |
| 174 | ExplosiveVegetation | Untested | |
| 175 | Gamble | Untested | |
| 176 | RunAmok | Untested | |
| 177 | HeatWave | Untested | |
| 178 | Fireblast | Untested | |
| 179 | Obliterate | Untested | |
| 180 | Wildfire | Untested | |
| 181 | Channel | Untested | |
| 182 | PlantOfSolitude | Untested | |
| 183 | EternalTreefolk | Untested | |
| 184 | JeelaiPlant | Untested | |
| 185 | CipplingVines | Untested | |
| 186 | PlantOfHerbs | Untested | |
| 187 | NaturesBlessing | Untested | |
| 188 | TreeSavant | Untested | |
| 189 | GrappleRoots | Untested | |
| 190 | SproutPlant | Untested | |
| 191 | VinePlant | Untested | |
| 192 | Treefice | Untested | |
| 193 | GiverOfPlants | Untested | |
| 194 | SproutAnArmy | Untested | |
| 195 | Planter | Untested | |
| 196 | TallTreefolk | Untested | |
| 197 | NaturalStatePlant | Untested | |
| 198 | PlantGrower | Untested | |
| 199 | Sproutlings | Untested | |
| 200 | PlantSprouter | Untested | |
| 201 | PlatePlant | Untested | |
| 202 | CliffsideSprout | Untested | |
| 203 | GlowingSpore | Untested | |
| 204 | DeadTree | Untested | |
| 205 | GiftOfNature | Untested | |
| 206 | TransparentPlant | Untested | |
| 207 | PerfectBog | Untested | |
| 208 | TreeGiant | Untested | |
| 209 | SpiritTree | Untested | |
| 210 | WarbriarStomper | Untested | |
| 211 | VerdictCommand | Untested | |
| 212 | Entangle | Untested | |
| 213 | Harvest | Untested | |
| 214 | LostSanctuary | Untested | |
| 215 | PlanterBox | Untested | |
| 216 | GrowTall | Untested | |
| 217 | Fertilize | Untested | |
| 218 | PlantofTrees | Untested | |
| 219 | SproutUp | Untested | |
| 220 | Overrun | Untested | |
| 221 | Uncover | Untested | |
| 222 | Simplify | Untested | |
| 223 | TreeGod | Untested | |
| 224 | Green-Sun | Untested | |
| 225 | Grow | Untested | |
| 226 | PlantSnap | Untested | |
| 227 | Gigatrunk | Untested | |
| 228 | DeeprootGuard | Untested | |
| 229 | MasterTree | Untested | |
| 230 | TreeOfLife | Untested | |
| 231 | Herblore | Untested | |
| 232 | Barrage | Untested | |
| 233 | FlameWave | Untested | |
| 234 | RitualOfDarkness | Untested | |
| 235 | Spectralize | Untested | |
| 236 | DreamBig | Untested | |
| 237 | AvalancheGolem | Untested | |
| 238 | CastAMold | Untested | |
| 239 | GroundTactics | Untested | |
| 240 | BreakThrough | Untested | |
| 241 | FoundationGolem | Untested | |
| 242 | GraniteGolem | Untested | |
| 243 | TargetDummy | Untested | |
| 244 | ShatteringSmash | Untested | |
| 245 | PutridFolks | Untested | |
| 246 | Riptide | Untested | |
| 247 | MerfolkFateseer | Untested | |
| 248 | BeachedMerfolk | Untested | |
| 249 | Legionaires | Untested | |
| 250 | SlipspaceMerfolk | Untested | |
| 251 | MerfolkElite | Untested | |
| 252 | MerfolkMage | Untested | |
| 253 | SkyScryerMerfolk | Untested | |
| 254 | Typhoon | Untested | |
| 255 | Rewind | Untested | |
| 256 | SpawnFish | Untested | |
| 257 | GeistOfDroolingTears | Untested | |
| 258 | RecurringNightmare | Untested | |
| 259 | SetStraight | Untested | |
| 260 | Spectral Amulet | Untested | |
| 261 | RestlessGhost | Untested | |
| 262 | BurstLightning | Untested | |
| 263 | GoblinGrunt | Untested | |
| 264 | CavalcadePyromancer | Untested | |
| 265 | Ringleader Champion | Untested | |
| 266 | SearingFire | Untested | |
| 267 | GoblinChanneler | Untested | |
| 268 | SearingGoblin | Untested | |
| 269 | GoblinLieutenant | Untested | |
| 270 | BlastOpen | Untested | |
| 271 | GoblinTactician | Untested | |
| 272 | AllOutAttackCommander | Untested | |
| 273 | UndyingDeathwood | Untested | |
| 274 | EndlessGarden | Untested | |
| 275 | UnendingSundew | Untested | |
| 276 | FromDust | Untested | |
| 277 | PottedFlower | Untested | |
| 278 | TreeOfAbundance | Untested | |
| 279 | GroundControl | Untested | |
| 280 | BloomingMarsh | Untested | |

---

## Test Session Log

### Session 1 - [Date]
Cards tested:
Results:

