using System;
using System.Linq;

using log4net;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader.FileTypes;
using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Database.Models.Shard;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Motion;
using ACE.Server.Network.Sequence;
using ACE.Server.Entity;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    public partial class Creature : Container
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Creature(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();

            EquippedObjectsLoaded = true;
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Creature(Biota biota) : base(biota)
        {
            SetEphemeralValues();

            // A player has their possessions items passed via the ctor. All other world objects must load their own equipped items
            if (!(this is Player))
                DatabaseManager.Shard.GetWieldedItems(biota.Id, AddBiotasToEquippedObjects);
        }

        private void SetEphemeralValues()
        {
            if (CreatureType == ACE.Entity.Enum.CreatureType.Human && !(WeenieClassId == 1 || WeenieClassId == 4))
                GenerateNewFace();

            if (CreatureType == ACE.Entity.Enum.CreatureType.Shadow)
                GenerateNewFace();

            // If any of the vitals don't exist for this biota, one will be created automatically in the CreatureVital ctor
            Vitals[PropertyAttribute2nd.MaxHealth] = new CreatureVital(this, PropertyAttribute2nd.MaxHealth);
            Vitals[PropertyAttribute2nd.MaxStamina] = new CreatureVital(this, PropertyAttribute2nd.MaxStamina);
            Vitals[PropertyAttribute2nd.MaxMana] = new CreatureVital(this, PropertyAttribute2nd.MaxMana);

            // If any of the attributes don't exist for this biota, one will be created automatically in the CreatureAttribute ctor
            Attributes[PropertyAttribute.Strength] = new CreatureAttribute(this, PropertyAttribute.Strength);
            Attributes[PropertyAttribute.Endurance] = new CreatureAttribute(this, PropertyAttribute.Endurance);
            Attributes[PropertyAttribute.Coordination] = new CreatureAttribute(this, PropertyAttribute.Coordination);
            Attributes[PropertyAttribute.Quickness] = new CreatureAttribute(this, PropertyAttribute.Quickness);
            Attributes[PropertyAttribute.Focus] = new CreatureAttribute(this, PropertyAttribute.Focus);
            Attributes[PropertyAttribute.Self] = new CreatureAttribute(this, PropertyAttribute.Self);

            foreach (var skillProperty in Biota.BiotaPropertiesSkill)
                Skills[(Skill)skillProperty.Type] = new CreatureSkill(this, (Skill)skillProperty.Type);

            if (Health.Current == 0)
                Health.Current = Health.MaxValue;
            if (Stamina.Current == 0)
                Stamina.Current = Stamina.MaxValue;
            if (Mana.Current == 0)
                Mana.Current = Mana.MaxValue;

            if (!(this is Player))
                GenerateWieldList();
        }


        public void GenerateNewFace()
        {
            var cg = DatManager.PortalDat.CharGen;

            if (!Heritage.HasValue)
            {
                if (!String.IsNullOrEmpty(HeritageGroup))
                {
                    HeritageGroup parsed = (HeritageGroup)Enum.Parse(typeof(HeritageGroup), HeritageGroup.Replace("'", ""));
                    if (parsed != 0)
                        Heritage = (int)parsed;
                }
            }

            if (!Gender.HasValue)
            {
                if (!String.IsNullOrEmpty(Sex))
                {
                    Gender parsed = (Gender)Enum.Parse(typeof(Gender), Sex);
                    if (parsed != 0)
                        Gender = (int)parsed;
                }
            }

            if (!Heritage.HasValue || !Gender.HasValue)
                return;

            SexCG sex = cg.HeritageGroups[(uint)Heritage].Genders[(int)Gender];

            PaletteBaseId = sex.BasePalette;

            Appearance appearance = new Appearance();

            appearance.HairStyle = 1;
            appearance.HairColor = 1;
            appearance.HairHue = 1;

            appearance.EyeColor = 1;
            appearance.Eyes = 1;

            appearance.Mouth = 1;
            appearance.Nose = 1;

            appearance.SkinHue = 1;

            // Get the hair first, because we need to know if you're bald, and that's the name of that tune!
            int size = sex.HairStyleList.Count / 3; // Why divide by 3 you ask? Because AC runtime generated characters didn't have much range in hairstyles.
            Random rand = new Random();
            appearance.HairStyle = (uint)rand.Next(size);

            HairStyleCG hairstyle = sex.HairStyleList[Convert.ToInt32(appearance.HairStyle)];
            bool isBald = hairstyle.Bald;

            size = sex.HairColorList.Count;
            appearance.HairColor = (uint)rand.Next(size);
            appearance.HairHue = rand.NextDouble();

            size = sex.EyeColorList.Count;
            appearance.EyeColor = (uint)rand.Next(size);
            size = sex.EyeStripList.Count;
            appearance.Eyes = (uint)rand.Next(size);

            size = sex.MouthStripList.Count;
            appearance.Mouth = (uint)rand.Next(size);

            size = sex.NoseStripList.Count;
            appearance.Nose = (uint)rand.Next(size);

            appearance.SkinHue = rand.NextDouble();

            //// Certain races (Undead, Tumeroks, Others?) have multiple body styles available. This is controlled via the "hair style".
            ////if (hairstyle.AlternateSetup > 0)
            ////    character.SetupTableId = hairstyle.AlternateSetup;

            if (!EyesTextureDID.HasValue)
                EyesTextureDID = sex.GetEyeTexture(appearance.Eyes, isBald);
            if (!DefaultEyesTextureDID.HasValue)
                DefaultEyesTextureDID = sex.GetDefaultEyeTexture(appearance.Eyes, isBald);
            if (!NoseTextureDID.HasValue)
                NoseTextureDID = sex.GetNoseTexture(appearance.Nose);
            if (!DefaultNoseTextureDID.HasValue)
                DefaultNoseTextureDID = sex.GetDefaultNoseTexture(appearance.Nose);
            if (!MouthTextureDID.HasValue)
                MouthTextureDID = sex.GetMouthTexture(appearance.Mouth);
            if (!DefaultMouthTextureDID.HasValue)
                DefaultMouthTextureDID = sex.GetDefaultMouthTexture(appearance.Mouth);
            if (!HairTextureDID.HasValue)
                HairTextureDID = sex.GetHairTexture(appearance.HairStyle);
            if (!DefaultHairTextureDID.HasValue)
                DefaultHairTextureDID = sex.GetDefaultHairTexture(appearance.HairStyle);
            if (!HeadObjectDID.HasValue)
                HeadObjectDID = sex.GetHeadObject(appearance.HairStyle);

            // Skin is stored as PaletteSet (list of Palettes), so we need to read in the set to get the specific palette
            var skinPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(sex.SkinPalSet);
            if (!SkinPaletteDID.HasValue)
                SkinPaletteDID = skinPalSet.GetPaletteID(appearance.SkinHue);

            // Hair is stored as PaletteSet (list of Palettes), so we need to read in the set to get the specific palette
            var hairPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(sex.HairColorList[Convert.ToInt32(appearance.HairColor)]);
            if (!HairPaletteDID.HasValue)
                HairPaletteDID = hairPalSet.GetPaletteID(appearance.HairHue);

            // Eye Color
            if (!EyesPaletteDID.HasValue)
                EyesPaletteDID = sex.EyeColorList[Convert.ToInt32(appearance.EyeColor)];
        }








        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************

        public CombatMode CombatMode { get; private set; }

        //public AceObject AceCorpse => AceObject;

        /// <summary>
        /// This will be false when creature is dead and waits for respawn
        /// </summary>
        public bool IsAlive { get; set; }

        public double RespawnTime { get; set; }

        public virtual void DoOnKill(Session killerSession)
        {
            OnKillInternal(killerSession).EnqueueChain();
        }

        protected ActionChain OnKillInternal(Session killerSession)
        {
            // Will start death animation
            OnKill(killerSession);

            // Wait, then run kill animation
            ActionChain onKillChain = new ActionChain();
            onKillChain.AddDelaySeconds(2);
            onKillChain.AddChain(GetCreateCorpseChain());

            return onKillChain;
        }

        protected virtual float GetCorpseSpawnTime()
        {
            return 60;
        }

        public ActionChain GetCreateCorpseChain()
        {
            ActionChain createCorpseChain = new ActionChain(this, () =>
            {
                ////// Create Corspe and set a location on the ground
                ////// TODO: set text of killer in description and find a better computation for the location, some corpse could end up in the ground
                ////var corpse = CorpseObjectFactory.CreateCorpse(this, this.Location);
                ////// FIXME(ddevec): We don't have a real corpse yet, so these come in null -- this hack just stops them from crashing the game
                ////corpse.Location.PositionY -= (corpse.ObjScale ?? 0);
                ////corpse.Location.PositionZ -= (corpse.ObjScale ?? 0) / 2;

                ////// Corpses stay on the ground for 5 * player level but minimum 1 hour
                ////// corpse.DespawnTime = Math.Max((int)session.Player.PropertiesInt[Enum.Properties.PropertyInt.Level] * 5, 360) + WorldManager.PortalYearTicks; // as in live
                ////// corpse.DespawnTime = 20 + WorldManager.PortalYearTicks; // only for testing
                ////float despawnTime = GetCorpseSpawnTime();

                ////// Create corpse
                ////CurrentLandblock.AddWorldObject(corpse);
                ////// Create corpse decay
                ////ActionChain despawnChain = new ActionChain();
                ////despawnChain.AddDelaySeconds(despawnTime);
                ////despawnChain.AddAction(CurrentLandblock, () => corpse.CurrentLandblock.RemoveWorldObject(corpse.Guid, false));
                ////despawnChain.EnqueueChain();
            });
            return createCorpseChain;
        }

        private void OnKill(Session session)
        {
            IsAlive = false;
            // This will determine if the derived type is a player
            var isDerivedPlayer = Guid.IsPlayer();

            if (!isDerivedPlayer)
            {
                // Create and send the death notice
                string killMessage = $"{session.Player.Name} has killed {Name}.";
                var creatureDeathEvent = new GameEventDeathNotice(session, killMessage);
                session.Network.EnqueueSend(creatureDeathEvent);
            }

            // MovementEvent: (Hand-)Combat or in the case of smite: from Standing to Death
            // TODO: Check if the duration of the motion can somehow be computed
            UniversalMotion motionDeath = new UniversalMotion(MotionStance.Standing, new MotionItem(MotionCommand.Dead));
            CurrentLandblock.EnqueueBroadcastMotion(this, motionDeath);

            // If the object is a creature, Remove it from from Landblock
            if (!isDerivedPlayer)
            {
                CurrentLandblock.RemoveWorldObject(Guid, false);
            }
        }

        /// <summary>
        /// This method checks to make sure we have a casting device equipped and if so, it sets
        /// the motion state and sends the messages to switch us to spellcasting state.   Og II
        /// </summary>
        public void HandleSwitchToMagicCombatMode()
        {
            HeldItem mEquipedWand = Children.Find(s => s.EquipMask == EquipMask.Held);
            if (mEquipedWand != null)
            {
                UniversalMotion mm = new UniversalMotion(MotionStance.Spellcasting);
                mm.MovementData.CurrentStyle = (uint)MotionStance.Spellcasting;
                SetMotionState(this, mm);
                CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessagePrivateUpdatePropertyInt(Sequences, PropertyInt.CombatMode, (int)CombatMode.Magic));
            }
            else
                log.InfoFormat("Changing combat mode for {0} - could not locate a wielded magic caster", Guid);
        }

        /// <summary>
        /// This method is called if we unwield missle ammo.   It will check to see if I have arrows wielded
        /// send the message to "hide" the arrow.
        /// </summary>
        /// <param name="oldCombatMode"></param>
        public void HandleUnloadMissileAmmo(CombatMode oldCombatMode)
        {
            // Before I can switch to any non missile mode, do I have missile ammo I need to remove?
            WorldObject ammo = null;
            HeldItem mEquipedAmmo = Children.Find(s => s.EquipMask == EquipMask.MissileAmmo);

            if (mEquipedAmmo != null)
                ammo = GetInventoryItem(new ObjectGuid(mEquipedAmmo.Guid));

            if (oldCombatMode == CombatMode.Missile)
            {
                if (ammo != null)
                {
                    ammo.Location = null;
                    CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessagePickupEvent(ammo));
                }
            }
        }

        /// <summary>
        /// This method sets us into peace mode.   It checks our current state to see if we have missle ammo equipped
        /// it will make the call to hid the "ammo" as we switch to peace mode.   It will then send the message switch our stance. Og II
        /// </summary>
        /// <param name="oldCombatMode"></param>
        /// <param name="isAutonomous"></param>
        public void HandleSwitchToPeaceMode(CombatMode oldCombatMode, bool isAutonomous = false)
        {
            HandleUnloadMissileAmmo(oldCombatMode);

            // FIXME: (Og II)<this is a hack for now to be removed.> Placement has an issue we have not figured out.   It has to do with animation frame. Og II
            PositionFlag &= ~UpdatePositionFlag.Placement;
            // End hack
            CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessageUpdatePosition(this));
            UniversalMotion mm = new UniversalMotion(MotionStance.Standing);
            mm.MovementData.CurrentStyle = (uint)MotionStance.Standing;
            SetMotionState(this, mm);
            var mEquipedAmmo = EquippedObjects.FirstOrDefault(s => s.Value.CurrentWieldedLocation == EquipMask.MissileAmmo).Value;
            CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessagePrivateUpdatePropertyInt(Sequences, PropertyInt.CombatMode, (int)CombatMode.NonCombat));
            if (mEquipedAmmo != null)
                CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectGhostRange, new GameMessagePickupEvent(mEquipedAmmo));
        }

        public void HandleSwitchToMissileCombatMode(ActionChain combatModeChain)
        {
            // TODO and FIXME: GetInventoryItem doesn't work for this so this function is effectively broke
            HeldItem mEquipedMissile = Children.Find(s => s.EquipMask == EquipMask.MissileWeapon);
            if (mEquipedMissile?.Guid != null)
            {
                WorldObject missileWeapon = GetInventoryItem(new ObjectGuid(mEquipedMissile.Guid));
                if (missileWeapon == null)
                {
                    log.InfoFormat("Changing combat mode for {0} - could not locate wielded weapon {1}", Guid, mEquipedMissile.Guid);
                    return;
                }

                var mEquipedAmmo = EquippedObjects.First(s => s.Value.CurrentWieldedLocation == EquipMask.MissileAmmo).Value;

                MotionStance ms;
                CombatStyle cs;

                if (missileWeapon.DefaultCombatStyle != null)
                    cs = missileWeapon.DefaultCombatStyle.Value;
                else
                {
                    log.InfoFormat("Changing combat mode for {0} - wielded item {1} has not be assigned a default combat style", Guid, mEquipedMissile.Guid);
                    return;
                }

                switch (cs)
                {
                    case CombatStyle.Bow:
                        ms = MotionStance.BowAttack;
                        break;
                    case CombatStyle.Crossbow:
                        ms = MotionStance.CrossBowAttack;
                        break;
                    default:
                        ms = MotionStance.Invalid;
                        break;
                }

                UniversalMotion mm = new UniversalMotion(ms);
                mm.MovementData.CurrentStyle = (ushort)ms;
                if (mEquipedAmmo == null)
                {
                    CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessageUpdatePosition(this));
                    SetMotionState(this, mm);
                }
                else
                {
                    CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessageUpdatePosition(this));
                    SetMotionState(this, mm);
                    mm.MovementData.ForwardCommand = (uint)MotionCommand.Reload;
                    SetMotionState(this, mm);
                    CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessageUpdatePosition(this));
                    // FIXME: (Og II)<this is a hack for now to be removed. Need to pull delay from dat file
                    combatModeChain.AddDelaySeconds(0.25);
                    // System.Threading.Thread.Sleep(250); // used for debugging
                    mm.MovementData.ForwardCommand = (ushort)MotionCommand.Invalid;
                    SetMotionState(this, mm);
                    // FIXME: (Og II)<this is a hack for now to be removed. Need to pull delay from dat file
                    combatModeChain.AddDelaySeconds(0.40);
                    combatModeChain.AddAction(this, () => CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessageParentEvent(this, mEquipedAmmo, 1, 1)));
                    // CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessageParentEvent(this, ammo, 1, 1)); // used for debugging
                }
                CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessagePrivateUpdatePropertyInt(Sequences, PropertyInt.CombatMode, (int)CombatMode.Missile));
            }
        }

        public void HandleSwitchToMeleeCombatMode(CombatMode olCombatMode)
        {
            // TODO and FIXME: GetInventoryItem doesn't work for this so this function is effectively broke
            bool shieldEquiped = false;
            bool weaponInShieldSlot = false;

            // Check to see if we were in missile combat and have an arrow hanging around we might need to remove.
            HandleUnloadMissileAmmo(olCombatMode);

            HeldItem mEquipedShieldSlot = Children.Find(s => s.EquipMask == EquipMask.Shield);
            if (mEquipedShieldSlot != null)
            {
                WorldObject itemInShieldSlot = GetInventoryItem(new ObjectGuid(mEquipedShieldSlot.Guid));
                if (itemInShieldSlot != null)
                {
                    if (itemInShieldSlot.ItemType == ItemType.Armor)
                        shieldEquiped = true;
                    else
                        weaponInShieldSlot = true;
                }
            }

            HeldItem mEquipedMelee = Children.Find(s => s.EquipMask == EquipMask.MeleeWeapon);
            HeldItem mEquipedTwoHanded = Children.Find(s => s.EquipMask == EquipMask.TwoHanded);
            MotionStance ms = MotionStance.Invalid;
            CombatStyle cs = CombatStyle.Undef;
            // are we unarmed?   If so, do we have a shield?
            if (mEquipedMelee == null && mEquipedTwoHanded == null && !weaponInShieldSlot)
            {
                if (!shieldEquiped)
                    ms = MotionStance.UaNoShieldAttack;
                else
                    ms = MotionStance.MeleeShieldAttack;
            }
            else if (weaponInShieldSlot)
                ms = MotionStance.DualWieldAttack;

            if (mEquipedTwoHanded != null)
            {
                WorldObject twoHandedWeapon = GetInventoryItem(new ObjectGuid(mEquipedTwoHanded.Guid));
                if (twoHandedWeapon.DefaultCombatStyle != null)
                {
                    cs = twoHandedWeapon.DefaultCombatStyle.Value;
                    // ms = MotionStance.TwoHandedSwordAttack;
                    // ms = MotionStance.TwoHandedStaffAttack; ?
                    switch (cs)
                    {
                        // case CombatStyle.???
                        // ms = MotionStance.TwoHandedStaffAttack;
                        // break;
                        default:
                            ms = MotionStance.TwoHandedSwordAttack;
                            break;
                    }
                }
            }

            // Let's see if we are melee single handed / two handed with our without shield as appropriate.
            if (mEquipedMelee?.Guid != null && ms != MotionStance.DualWieldAttack)
            {
                WorldObject meleeWeapon = GetInventoryItem(new ObjectGuid(mEquipedMelee.Guid));

                if (meleeWeapon == null)
                {
                    log.InfoFormat("Changing combat mode for {0} - could not locate wielded weapon {1}", Guid, mEquipedMelee.Guid);
                    return;
                }

                if (!shieldEquiped)
                {
                    if (meleeWeapon.DefaultCombatStyle != null)
                    {
                        cs = meleeWeapon.DefaultCombatStyle.Value;
                        switch (cs)
                        {
                            case CombatStyle.Atlatl:
                                ms = MotionStance.AtlatlCombat;
                                break;
                            case CombatStyle.Sling:
                                ms = MotionStance.SlingAttack;
                                break;
                            case CombatStyle.ThrownWeapon:
                                ms = MotionStance.ThrownWeaponAttack;
                                break;
                            default:
                                ms = MotionStance.MeleeNoShieldAttack;
                                break;
                        }
                    }
                }
                else
                {
                    switch (meleeWeapon.DefaultCombatStyle)
                    {
                        case CombatStyle.Unarmed:
                        case CombatStyle.OneHanded:
                        case CombatStyle.OneHandedAndShield:
                        case CombatStyle.TwoHanded:
                        case CombatStyle.DualWield:
                        case CombatStyle.Melee:
                            ms = MotionStance.MeleeShieldAttack;
                            break;
                        case CombatStyle.ThrownWeapon:
                            ms = MotionStance.ThrownShieldCombat;
                            break;
                        ////case CombatStyle.Unarmed:
                        ////    ms = MotionStance.MeleeShieldAttack;
                        ////    break;
                        default:
                            log.InfoFormat(
                                "Changing combat mode for {0} - unable to determine correct combat stance for weapon {1}", Guid, mEquipedMelee.Guid);
                            return;
                    }
                }
            }
            if (ms != MotionStance.Invalid)
            {
                UniversalMotion mm = new UniversalMotion(ms);
                mm.MovementData.CurrentStyle = (ushort)ms;
                SetMotionState(this, mm);
                CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessagePrivateUpdatePropertyInt(Sequences, PropertyInt.CombatMode, (int)CombatMode.Melee));
            }
            else
                log.InfoFormat("Changing combat mode for {0} - wielded item {1} has not be assigned a default combat style", Guid, mEquipedMelee?.Guid ?? mEquipedTwoHanded?.Guid);
        }

        public void SetCombatMode(CombatMode newCombatMode)
        {
            log.InfoFormat("Changing combat mode for {0} to {1}", Guid, newCombatMode);

            ActionChain combatModeChain = new ActionChain();
            combatModeChain.AddAction(this, () =>
                {
                    CombatMode oldCombatMode = CombatMode;
                    CombatMode = newCombatMode;
                    switch (CombatMode)
                    {
                        case CombatMode.NonCombat:
                            HandleSwitchToPeaceMode(oldCombatMode);
                            break;
                        case CombatMode.Melee:
                            HandleSwitchToMeleeCombatMode(oldCombatMode);
                            break;
                        case CombatMode.Magic:
                            HandleSwitchToMagicCombatMode();
                            break;
                        case CombatMode.Missile:
                            HandleSwitchToMissileCombatMode(combatModeChain);
                            break;
                        default:
                            log.InfoFormat("Changing combat mode for {0} - something has gone wrong", Guid);
                            break;
                    }
                });
            combatModeChain.EnqueueChain();
        }

        public void SetMotionState(WorldObject obj, MotionState motionState)
        {
            CurrentMotionState = motionState;
            motionState.IsAutonomous = false;
            GameMessageUpdateMotion updateMotion = new GameMessageUpdateMotion(Guid, Sequences.GetCurrentSequence(SequenceType.ObjectInstance), obj.Sequences, motionState);
            CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, updateMotion);
        }


        /// <summary>
        /// This signature services MoveToObject and TurnToObject
        /// Update Position prior to start, start them moving or turning, set statemachine to moving.
        /// Moved from player - we need to be able to move creatures as well.   Og II
        /// </summary>
        /// <param name="worldObjectPosition">Position in the world</param>
        /// <param name="sequence">Sequence for the object getting the message.</param>
        /// <param name="movementType">What type of movement are we about to execute</param>
        /// <param name="targetGuid">Who are we moving or turning toward</param>
        /// <returns>MovementStates</returns>
        public void OnAutonomousMove(ACE.Entity.Position worldObjectPosition, SequenceManager sequence, MovementTypes movementType, ObjectGuid targetGuid)
        {
            UniversalMotion newMotion = new UniversalMotion(MotionStance.Standing, worldObjectPosition, targetGuid);
            newMotion.DistanceFrom = 0.60f;
            newMotion.MovementTypes = movementType;
            CurrentLandblock.EnqueueBroadcast(Location, Landblock.MaxObjectRange, new GameMessageUpdatePosition(this));
            CurrentLandblock.EnqueueBroadcastMotion(this, newMotion);
        }

        /// <summary>
        /// This is the OnUse method.   This is just an initial implemention.   I have put in the turn to action at this point.
        /// If we are out of use radius, move to the object.   Once in range, let's turn the creature toward us and get started.
        /// Note - we may need to make an NPC class vs monster as using a monster does not make them turn towrad you as I recall. Og II
        ///  Also, once we are reading in the emotes table by weenie - this will automatically customize the behavior for creatures.
        /// </summary>
        /// <param name="playerId">Identity of the player we are interacting with</param>
        public override void ActOnUse(ObjectGuid playerId)
        {
            Player player = CurrentLandblock.GetObject(playerId) as Player;
            if (player == null)
            {
                return;
            }

            if (!player.IsWithinUseRadiusOf(this))
            {
                player.DoMoveTo(this);
            }
            else
            {
                OnAutonomousMove(player.Location, this.Sequences, MovementTypes.TurnToObject, playerId);

                GameEventUseDone sendUseDoneEvent = new GameEventUseDone(player.Session);
                player.Session.Network.EnqueueSend(sendUseDoneEvent);
            }
        }       

        protected static readonly UniversalMotion MotionDeath = new UniversalMotion(MotionStance.Standing, new MotionItem(MotionCommand.Dead));
    }
}
