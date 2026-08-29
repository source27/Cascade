using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cascade.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ScaleButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Transform _target;
        [SerializeField] [Range(0.1f, 2f)] private float _pressedScale = 0.9f;
        [SerializeField] private float _duration = 0.1f;

        private Button _button;
        private Vector3 _restScale = Vector3.one;
        private MotionHandle _motion;
        private bool _pressed;

        private Transform Target => _target != null ? _target : transform;

        private void Reset()
        {
            _target = transform;
            ApplyButtonTransition();
        }

        private void OnValidate()
        {
            _duration = Mathf.Max(0f, _duration);
            ApplyButtonTransition();
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_target == null)
                _target = transform;
            ApplyButtonTransition();
            _restScale = Target.localScale;
        }

        private void OnEnable()
        {
            if (_button == null)
                _button = GetComponent<Button>();
            _restScale = Target.localScale;
            _pressed = false;
        }

        private void OnDisable()
        {
            CancelMotion();
            Target.localScale = _restScale;
            _pressed = false;
        }

        private void Update()
        {
            // 按下过程中被禁用时立即回弹，避免卡在缩小态。
            if (_pressed && !CanScale)
                Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanScale)
                return;
            _pressed = true;
            AnimateTo(_restScale * _pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        /// <summary>Button.IsInteractable 含 CanvasGroup；不可交互时不缩放。</summary>
        private bool CanScale =>
            isActiveAndEnabled && _button != null && _button.IsInteractable();

        private void Release()
        {
            if (!_pressed)
                return;
            _pressed = false;
            AnimateTo(_restScale);
        }


        private void AnimateTo(Vector3 scale)
        {
            var target = Target;
            CancelMotion();
            if (_duration <= 0f)
            {
                target.localScale = scale;
                return;
            }

            _motion = LMotion.Create(target.localScale, scale, _duration)
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(target);
        }

        private void CancelMotion()
        {
            _motion.TryCancel();
            _motion = MotionHandle.None;
        }

        private void ApplyButtonTransition()
        {
            var button = _button != null ? _button : GetComponent<Button>();
            if (button != null)
                button.transition = Selectable.Transition.None;
        }
    }
}
