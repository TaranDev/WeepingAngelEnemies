using UnityEngine;
using EntityStates;
using RoR2;

namespace WeepingAngelEnemies
{
    public class AngelFreezeState : BaseState
    {

        public static GameObject frozenEffectPrefab;

        private TemporaryOverlayInstance temporaryOverlay;

        private Animator animator;

        CharacterModel characterModel;
        public override void OnEnter()
        {
            base.OnEnter();
            FreezeCharacter(true);
        }

        public override void OnExit()
        {
            FreezeCharacter(false);

            if (animator)
            {
                animator.enabled = true;
            }

            if (temporaryOverlay != null)
            {
                temporaryOverlay.RemoveFromCharacterModel();
                TemporaryOverlayManager.RemoveOverlay(temporaryOverlay.managerIndex);
                temporaryOverlay.Destroy();
                temporaryOverlay = null;
            }
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
        }

        private void FreezeCharacter(bool isEnabled)
        {
            
            Transform modelTransform = GetModelTransform();
            if ((bool)modelTransform)
            {
                CharacterModel component = modelTransform.GetComponent<CharacterModel>();
                if ((bool)component)
                {
                    if (isEnabled && WeepingAngelEnemies.enemiesLookLikeStatues.Value)
                    {
                        temporaryOverlay = TemporaryOverlayManager.AddOverlay(base.gameObject);
                        //temporaryOverlay.duration = 1f;
                        Material frozenMat = LegacyResourcesAPI.Load<Material>("Materials/matIsFrozen");
                        frozenMat.color = Color.grey;
                        temporaryOverlay.originalMaterial = frozenMat;
                        temporaryOverlay.AddToCharacterModel(component);
                    } else
                    {
                        if (temporaryOverlay != null)
                        {
                            temporaryOverlay.RemoveFromCharacterModel();
                            TemporaryOverlayManager.RemoveOverlay(temporaryOverlay.managerIndex);
                            temporaryOverlay.Destroy();
                            temporaryOverlay = null;
                        }
                    }
                    
                }
            }

            if (base.rigidbody && !base.rigidbody.isKinematic)
            {
                base.rigidbody.velocity = Vector3.zero;
            }
            if (base.rigidbodyMotor)
            {
                base.rigidbodyMotor.enabled = !isEnabled;
                base.rigidbodyMotor.moveVector = Vector3.zero;
            }
            //base.healthComponent.isInFrozenState = isEnabled;
            if (base.characterDirection)
            {
                base.characterDirection.moveVector = base.characterDirection.forward;
                base.characterDirection.enabled = !isEnabled;
            }
            if (base.characterMotor)
            {
                base.characterMotor.enabled = !isEnabled;
            }
            animator = base.GetModelAnimator();
            if (animator)
            {
                animator.enabled = !isEnabled;
            } else
            {
                Destroy(base.gameObject);
            }
        }


        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }
    }
}