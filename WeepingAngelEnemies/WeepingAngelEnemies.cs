using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RiskOfOptions;
using RiskOfOptions.Options;
using RiskOfOptions.OptionConfigs;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Networking;
using EntityStates;


namespace WeepingAngelEnemies
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class WeepingAngelEnemies : BaseUnityPlugin
    {

        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "TaranDev";
        public const string PluginName = "WeepingAngelEnemies";
        public const string PluginVersion = "1.0.0";

        public void Awake()
        {
            Log.Init(Logger);
            configs();
            visibleCharacterMasters = new List<VisibleCharacterMaster>();
        }

        public void OnEnable()
        {
            On.RoR2.CharacterBody.RecalculateStats += RecalculateStats;
            On.RoR2.GlobalEventManager.OnCharacterDeath += CharacterDeath;
            On.RoR2.WormBodyPositions.FixedUpdate += WormUpdate;
            On.RoR2.WormBodyPositions2.Update += WormUpdate2;
            On.RoR2.WormBodyPositionsDriver.FixedUpdate += WormUpdateDriver;
        }

        private void WormUpdateDriver(On.RoR2.WormBodyPositionsDriver.orig_FixedUpdate orig, WormBodyPositionsDriver self)
        {
            CharacterBody body = self.gameObject.GetComponent<CharacterBody>();
            if (((IsVisibileEnemy(body) && !body.isBoss && freezeEnemiesUponLook.Value) || (IsVisibileEnemy(body) && body.isBoss && shouldFreezeBosses.Value)) && body.healthComponent.alive)
            {
                return;
            }
            orig(self);
        }

        private void WormUpdate2(On.RoR2.WormBodyPositions2.orig_Update orig, WormBodyPositions2 self)
        {
            CharacterBody body = self.gameObject.GetComponent<CharacterBody>();
            if (((IsVisibileEnemy(body) && !body.isBoss && freezeEnemiesUponLook.Value) || (IsVisibileEnemy(body) && body.isBoss && shouldFreezeBosses.Value)) && body.healthComponent.alive)
            {
                return;
            }
            orig(self);
        }

        private void WormUpdate(On.RoR2.WormBodyPositions.orig_FixedUpdate orig, WormBodyPositions self)
        {
            CharacterBody body = self.gameObject.GetComponent<CharacterBody>();
            if (((IsVisibileEnemy(body) && !body.isBoss && freezeEnemiesUponLook.Value) || (IsVisibileEnemy(body) && body.isBoss && shouldFreezeBosses.Value)) && body.healthComponent.alive)
            {
                return;
            }
            orig(self);
        }

        public void OnDisable()
        {
            On.RoR2.CharacterBody.RecalculateStats -= RecalculateStats;
            On.RoR2.GlobalEventManager.OnCharacterDeath -= CharacterDeath;
            On.RoR2.WormBodyPositions.FixedUpdate -= WormUpdate;
        }

        private void CharacterDeath(On.RoR2.GlobalEventManager.orig_OnCharacterDeath orig, GlobalEventManager self, DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport == null)
            {
                return;
            }
            DamageInfo damageInfo = damageReport.damageInfo;
            GameObject gameObject = null;
            if ((bool)damageReport.victim)
            {
                gameObject = damageReport.victim.gameObject;
            }
            CharacterBody victimBody = damageReport.victimBody;
            TeamComponent teamComponent = null;
            CharacterMaster victimMaster = damageReport.victimMaster;
            CharacterDeathBehavior component;

            if (victimBody)
            {
                component = victimBody.GetComponent<CharacterDeathBehavior>();

                if (victimMaster != null)
                {
                    var entityStateMachines = victimBody.GetComponents<EntityStateMachine>();
                    Log.Info("DeathState: " + component.deathState);
                    if (entityStateMachines != null && entityStateMachines.Length > 0)
                    {
                        foreach (var entityStateMachine in entityStateMachines)
                        {
                            Log.Info(entityStateMachine.name);
                            if (entityStateMachine.state is AngelFreezeState)
                            {
                                entityStateMachine.SetInterruptState(EntityStateCatalog.InstantiateState(ref component.deathState), InterruptPriority.Death);
                            }
                        }
                    }
                }
            }


            orig(self, damageReport);
        }

        private void RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);

            Vector3 enemyPosition;

            if (self.corePosition != null)
            {
                enemyPosition = self.corePosition;
            }
            else if (self.footPosition != null)
            {
                enemyPosition = self.footPosition;
            }
            else
            {
                enemyPosition = self.gameObject.transform.position;
            }

            if (!self.isPlayerControlled)
            {
                if((!self.isBoss && !self.name.Contains("Brother")) || (self.isBoss && !self.name.Contains("Brother") && speedBoostBosses.Value) || (self.name.Contains("Brother") && speedBoostMithrix.Value))
                {
                    List<PlayerCharacterMasterController> players = PlayerCharacterMasterController._instances;
                    foreach (PlayerCharacterMasterController player in players)
                    {
                        if(player.master && player.body)
                        {
                            if (player.master.teamIndex == TeamIndex.Player && player.body.isPlayerControlled)
                            {
                                CharacterBody body = player.body;
                                float distance = Vector3.Distance(body.corePosition, enemyPosition);
                                if (distance < enemyAttackRange.Value)
                                {
                                    return;
                                }
                            }
                        }
                    }
                    self.moveSpeed = self.moveSpeed * enemySpeedBoost.Value;
                    self.attackSpeed = self.attackSpeed * enemyAttackSpeedBoost.Value;
                }
            }
        }

        List<VisibleCharacterMaster> visibleCharacterMasters;

        void Update()
        {
            if (Run.instance != null)
            {
                UpdateVisibleCharacterMasters();

                if (freezeEnemiesUponLook.Value)
                {
                    FreezeEnemies();
                }
            }
            else
            {
                if (visibleCharacterMasters != null)
                {
                    visibleCharacterMasters.Clear();
                }
            }
        }

        void UpdateVisibleCharacterMasters()
        {
            List<CharacterMaster> characterMasters = CharacterMaster.instancesList;
            List<CharacterMaster> currentVisibleCharacterMasters = new List<CharacterMaster>();

            foreach (CharacterMaster character in characterMasters)
            {
                if(character != null && character.GetBody() != null)
                {
                    if (IsVisibileEnemy(character.GetBody()))
                    {
                        currentVisibleCharacterMasters.Add(character);

                    }
                }
            }

            List<VisibleCharacterMaster> newVisibleCharacterMasters = new List<VisibleCharacterMaster>();

            if (currentVisibleCharacterMasters.Count > 0 || visibleCharacterMasters.Count > 0)
            {
                foreach (VisibleCharacterMaster visibleCharacterMaster in visibleCharacterMasters)
                {
                    if (currentVisibleCharacterMasters.Contains(visibleCharacterMaster.characterMaster))
                    {
                        // Already visible character is still visible
                        
                        currentVisibleCharacterMasters.Remove(visibleCharacterMaster.characterMaster);
                        newVisibleCharacterMasters.Add(visibleCharacterMaster);
                    }
                    else
                    {
                        // Previously visible character is no longer visible
                        if(visibleCharacterMaster.characterMaster)
                        {
                            UnfreezeEnemy(visibleCharacterMaster.characterMaster);
                        }
                    }
                }

                // Remaining is new character masters
                foreach (CharacterMaster character in currentVisibleCharacterMasters)
                {
                    newVisibleCharacterMasters.Add(new VisibleCharacterMaster(character, Run.TimeStamp.now.t, false));
                }

                visibleCharacterMasters = newVisibleCharacterMasters;
            }

        }

        bool IsVisibileEnemy(CharacterBody characterBody)
        {
            
            if (characterBody.teamComponent.teamIndex == TeamIndex.Monster || characterBody.teamComponent.teamIndex == TeamIndex.Lunar || characterBody.teamComponent.teamIndex == TeamIndex.Void)
            {

                Vector3 enemyPosition;

                if (characterBody.corePosition != null)
                {
                    enemyPosition = characterBody.corePosition;
                }
                else if (characterBody.footPosition != null)
                {
                    enemyPosition = characterBody.footPosition;
                }
                else
                {
                    enemyPosition = characterBody.gameObject.transform.position;
                }

                Vector3 screenPoint;

                screenPoint = Camera.main.WorldToViewportPoint(enemyPosition);

                bool onScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;

                List<PlayerCharacterMasterController> players = PlayerCharacterMasterController._instances;

                if (characterViewPoint.Value)
                {
                    // In front of character

                    foreach (PlayerCharacterMasterController player in players)
                    {
                        if (player.master.teamIndex == TeamIndex.Player && player.body.isPlayerControlled)
                        {
                            CharacterBody body = player.body;
                            var heading = enemyPosition - body.corePosition;
                            float dot = Vector3.Dot(heading, Camera.main.transform.forward);
                            float distance = Vector3.Distance(body.corePosition, enemyPosition);

                            Vector3 targetDir = enemyPosition - body.corePosition;
                            float angle = Vector3.Angle(targetDir, Camera.main.transform.forward);

                            if (angle < characterFOV.Value && distance > enemyAttackRange.Value)
                            {
                                return true && onScreen;
                            }
                        }
                    }
                    return false;
                }
                else
                {
                    // Anywhere on screen
                    foreach (PlayerCharacterMasterController player in players)
                    {
                        if (player.master.teamIndex == TeamIndex.Player && player.body.isPlayerControlled)
                        {
                            CharacterBody body = player.body;
                            float distance = Vector3.Distance(body.corePosition, enemyPosition);
                            if (distance < enemyAttackRange.Value)
                            {
                                return false;
                            }
                        }
                    }
                    
                    return onScreen;
                }
                
            }
            return false;
        }



        void FreezeEnemies()
        {
            foreach (VisibleCharacterMaster visibleCharacterMaster in visibleCharacterMasters)
            {
                CharacterMaster characterMaster = visibleCharacterMaster.characterMaster;
                CharacterBody body = characterMaster.GetBody();
                float timeFirstVisible = visibleCharacterMaster.timeFirstVisible;

                if((Run.TimeStamp.now.t - timeFirstVisible) > enemyReactionTime.Value)
                {
                    // after reaction time

                    HealthComponent healthComponent = body.healthComponent;
                    if (healthComponent)
                    {
                        if (((!body.isBoss && !body.name.Contains("Brother")) || (body.isBoss && shouldFreezeBosses.Value && !body.name.Contains("Brother")) || ((body.name.Contains("Brother") && shouldFreezeMithrix.Value))) && !visibleCharacterMaster.frozen && body.healthComponent.alive)
                        {
                            visibleCharacterMaster.frozen = true;
                            FreezeEnemy(characterMaster);
                        }
                    }
                }
            }
        }

        void FreezeEnemy(CharacterMaster characterMaster)
        {
            var entityStateMachines = characterMaster.GetBody().GetComponents<EntityStateMachine>();
            if (entityStateMachines != null && entityStateMachines.Length > 0)
            {
                foreach (var entityStateMachine in entityStateMachines)
                {
                    if (!(entityStateMachine.state is AngelFreezeState))
                    {
                        CharacterBody body = characterMaster.GetBody();
                        if (body)
                        {
                            CharacterDeathBehavior component = body.GetComponent<CharacterDeathBehavior>();

                            if (component && entityStateMachine.state.GetType() != component.deathState.stateType)
                            {
                                AngelFreezeState stunState2 = new AngelFreezeState();
                                entityStateMachine.SetInterruptState(stunState2, InterruptPriority.Death);
                            }
                        }
                    }
                }
            }
        }

        void UnfreezeEnemy(CharacterMaster characterMaster)
        {
            if(characterMaster != null && characterMaster.GetBody() != null)
            {
                var entityStateMachines = characterMaster.GetBody().GetComponents<EntityStateMachine>();
                if (entityStateMachines != null && entityStateMachines.Length > 0)
                {
                    foreach (var entityStateMachine in entityStateMachines)
                    {
                        if (entityStateMachine.state is AngelFreezeState)
                        {
                            entityStateMachine.SetInterruptState(EntityStateCatalog.InstantiateState(ref entityStateMachine.mainStateType), InterruptPriority.Death);
                        }
                    }
                }
            }
        }

        

        private void RefreshFreezes()
        {
            foreach (VisibleCharacterMaster visibleCharacterMaster in visibleCharacterMasters)
            {
                CharacterMaster characterMaster = visibleCharacterMaster.characterMaster;
                CharacterBody body = characterMaster.GetBody();
                float timeFirstVisible = visibleCharacterMaster.timeFirstVisible;

                if ((Run.TimeStamp.now.t - timeFirstVisible) > enemyReactionTime.Value)
                {
                    // after reaction time

                    HealthComponent healthComponent = body.healthComponent;
                    if (healthComponent)
                    {
                        if (((!body.isBoss && !body.name.Contains("Brother")) || (body.isBoss && shouldFreezeBosses.Value && !body.name.Contains("Brother")) || ((body.name.Contains("Brother") && shouldFreezeMithrix.Value))))
                        {
                            visibleCharacterMaster.frozen = true;
                            var entityStateMachines = characterMaster.GetBody().GetComponents<EntityStateMachine>();
                            if (entityStateMachines != null && entityStateMachines.Length > 0)
                            {
                                foreach (var entityStateMachine in entityStateMachines)
                                {
                                    if (entityStateMachine.state is AngelFreezeState)
                                    {
                                        if (body)
                                        {
                                            CharacterDeathBehavior component = body.GetComponent<CharacterDeathBehavior>();

                                            if (component && entityStateMachine.state.GetType() != component.deathState.stateType)
                                            {
                                                entityStateMachine.state.OnExit();
                                                entityStateMachine.SetInterruptState(EntityStateCatalog.InstantiateState(ref entityStateMachine.mainStateType), InterruptPriority.Death);
                                                AngelFreezeState stunState2 = new AngelFreezeState();
                                                entityStateMachine.SetInterruptState(stunState2, InterruptPriority.Death);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        public static ConfigEntry<float> enemySpeedBoost;

        public static ConfigEntry<float> enemyAttackSpeedBoost;

        public static ConfigEntry<float> enemyReactionTime;

        public static ConfigEntry<float> enemyAttackRange;

        public static ConfigEntry<bool> enemiesLookLikeStatues;

        public static ConfigEntry<bool> freezeEnemiesUponLook;

        public static ConfigEntry<bool> shouldFreezeBosses;

        public static ConfigEntry<bool> shouldFreezeMithrix;

        public static ConfigEntry<bool> speedBoostBosses;

        public static ConfigEntry<bool> speedBoostMithrix;

        public static ConfigEntry<bool> characterViewPoint;

        public static ConfigEntry<float> characterFOV;

        private void configs()
        {
            enemySpeedBoost = Config.Bind("General", "Enemy Speed Boost", 10f, "The boost to enemy speed when not looking at them.\nDefault is 10.");
            ModSettingsManager.AddOption(new StepSliderOption(enemySpeedBoost,
                new StepSliderConfig
                {
                    min = 1f,
                    max = 100f,
                    increment = 1f
                }));

            enemyAttackSpeedBoost = Config.Bind("General", "Enemy Attack Speed Boost", 2f, "The boost to enemy attack speed when not looking at them.\nDefault is 2.");
            ModSettingsManager.AddOption(new StepSliderOption(enemyAttackSpeedBoost,
                new StepSliderConfig
                {
                    min = 1f,
                    max = 100f,
                    increment = 1f
                }));

            enemyReactionTime = Config.Bind("General", "Enemy Reaction Time", 0f, "How many seconds it takes for enemies to freeze after being seen.\nDefault is 0.");
            ModSettingsManager.AddOption(new StepSliderOption(enemyReactionTime,
                new StepSliderConfig
                {
                    min = 0f,
                    max = 5f,
                    increment = 0.1f
                }));

            enemyAttackRange = Config.Bind("General", "Enemy Attack Range", 3f, "How close enemies need to get to you before they can attack while being seen. Enemies will have normal move speed within this range. Setting this to 0 may cause some melee enemies to never land a hit.\nDefault is 3.");
            ModSettingsManager.AddOption(new StepSliderOption(enemyAttackRange,
                new StepSliderConfig
                {
                    min = 0f,
                    max = 50f,
                    increment = 1f
                }));

            enemiesLookLikeStatues = Config.Bind("General", "Frozen enemies look like statues", true, "If enemies should look like statues when frozen.\nDefault is true.");
            ModSettingsManager.AddOption(new CheckBoxOption(enemiesLookLikeStatues));

            enemiesLookLikeStatues.SettingChanged += (o, args) =>
            {
                if (freezeEnemiesUponLook.Value)
                {
                    RefreshFreezes();
                }
            };

            freezeEnemiesUponLook = Config.Bind("General", "Freeze enemies when looking at them", true, "If enemies should freeze when you look at them.\nDefault is true.");
            ModSettingsManager.AddOption(new CheckBoxOption(freezeEnemiesUponLook));

            shouldFreezeBosses = Config.Bind("General", "Freeze Bosses", false, "If bosses should also be frozen when looked at.\nDefault is false.");
            ModSettingsManager.AddOption(new CheckBoxOption(shouldFreezeBosses));

            shouldFreezeMithrix = Config.Bind("General", "Freeze Mithrix", false, "If mithrix should also be frozen when looked at.\nDefault is false.");
            ModSettingsManager.AddOption(new CheckBoxOption(shouldFreezeMithrix));

            speedBoostBosses = Config.Bind("General", "Stats Boost Bosses", false, "If bosses should get the enemy speed and attack speed boost applied to them.\nDefault is false.");
            ModSettingsManager.AddOption(new CheckBoxOption(speedBoostBosses));

            speedBoostMithrix = Config.Bind("General", "Stats Boost Mithrix", false, "If Mithrix should get the enemy speed and attack speed boost applied to them.\nDefault is false.");
            ModSettingsManager.AddOption(new CheckBoxOption(speedBoostMithrix));

            shouldFreezeBosses.SettingChanged += (o, args) =>
            {
                if (freezeEnemiesUponLook.Value)
                {
                    RefreshFreezes();
                }

            };

            characterViewPoint = Config.Bind("General", "Use Character POV", true, "If enemies will freeze when visible from the characters eyes, instead of visible from the third person camera. Turning this off will cause enemies to freeze when they appear anywhere on the screen, which includes the small section on the bottom of the screen behind the character. This will freeze most short range melee enemies before they can attack. Keeping this on is recommended.\nDefault is true.");
            ModSettingsManager.AddOption(new CheckBoxOption(characterViewPoint));

            characterFOV = Config.Bind("General", "Character FOV", 50f, "The characters Field of View. Enemies will freeze within this and move outside of it.\nDefault is 50.");
            ModSettingsManager.AddOption(new StepSliderOption(characterFOV,
                new StepSliderConfig
                {
                    min = 0f,
                    max = 100f,
                    increment = 1f
                }));
        }
    }

    public class VisibleCharacterMaster
    {
        public CharacterMaster characterMaster { get; set; }
        public float timeFirstVisible { get; set; }
        public bool frozen { get; set; }
        public VisibleCharacterMaster(CharacterMaster characterMaster, float timeFirstVisible, bool frozen)
        {
            this.characterMaster = characterMaster;
            this.timeFirstVisible = timeFirstVisible;
            this.frozen = frozen;
        }

        public override string ToString() => "CharacterMaster: " + characterMaster.name + ", TimeFirstVisible: " + timeFirstVisible;
    }
}