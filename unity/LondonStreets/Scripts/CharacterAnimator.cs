using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LondonStreets
{
    /// <summary>
    /// Plays a character's idle, walk and run animations and blends between
    /// them, without needing an Animator Controller. Works with the clips that
    /// came inside the model file, or any clips you drag into the Inspector.
    /// </summary>
    public class CharacterAnimator
    {
        public enum State { Idle, Walk, Run }

        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly AnimationClipPlayable[] _playables = new AnimationClipPlayable[3];
        readonly AnimationClip[] _clips = new AnimationClip[3];
        readonly float[] _weights = new float[3];
        int _active;
        bool _frozen;

        public bool IsValid => _graph.IsValid();

        /// <summary>
        /// Sets up animation on <paramref name="model"/>. Returns null if there's
        /// no walk animation to play.
        /// </summary>
        public static CharacterAnimator Create(GameObject model, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            if (walk == null)
                return null;
            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                // Put the Animator on the copy of the model (inside the wrapper).
                var target = model.transform.childCount > 0 ? model.transform.GetChild(0).gameObject : model;
                animator = target.AddComponent<Animator>();
            }
            // The animations are played directly, so a controller that came with the model would only get in the way.
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var result = new CharacterAnimator();
            result._clips[0] = idle;
            result._clips[1] = walk;
            result._clips[2] = run != null ? run : walk;
            result._graph = PlayableGraph.Create(model.name + " animation");
            result._graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            result._mixer = AnimationMixerPlayable.Create(result._graph, 3);
            for (int i = 0; i < 3; i++)
            {
                if (result._clips[i] == null)
                    continue;
                result._playables[i] = AnimationClipPlayable.Create(result._graph, result._clips[i]);
                result._graph.Connect(result._playables[i], 0, result._mixer, i);
            }
            var output = AnimationPlayableOutput.Create(result._graph, "Animation", animator);
            output.SetSourcePlayable(result._mixer);
            result._weights[1] = 1f;
            result._active = 1;
            result._mixer.SetInputWeight(1, 1f);
            result._graph.Play();
            return result;
        }

        /// <summary>Blends towards <paramref name="state"/>. Call every frame.</summary>
        public void Update(State state, float deltaTime)
        {
            if (!_graph.IsValid())
                return;
            int wanted = (int)state;
            // No idle animation: freeze the walk instead of walking on the spot.
            bool freeze = state == State.Idle && _clips[0] == null;
            if (freeze)
                wanted = _active;
            if (freeze != _frozen)
            {
                _frozen = freeze;
                for (int i = 0; i < 3; i++)
                {
                    if (_clips[i] != null)
                        _playables[i].SetSpeed(freeze ? 0.0 : 1.0);
                }
            }
            if (_clips[wanted] == null)
                wanted = 1;
            if (wanted != _active && _weights[_active] > 0.99f)
            {
                // Start the new animation from the beginning.
                _playables[wanted].SetTime(0);
            }
            _active = wanted;

            float step = deltaTime / 0.2f;
            for (int i = 0; i < 3; i++)
            {
                if (_clips[i] == null)
                    continue;
                _weights[i] = Mathf.MoveTowards(_weights[i], i == _active ? 1f : 0f, step);
                _mixer.SetInputWeight(i, _weights[i]);
                // Make every animation repeat instead of stopping after one go.
                var clip = _clips[i];
                if (!clip.isLooping && clip.length > 0f)
                {
                    double time = _playables[i].GetTime();
                    if (time > clip.length)
                        _playables[i].SetTime(time % clip.length);
                }
            }
        }

        public void Destroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
