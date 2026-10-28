using Unity.Physics.Authoring;
using Unity.Physics;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CraftObject : MonoBehaviour
{
    [SerializeField]
    private string _ownerGUID;

    public string OwnerGUID => _ownerGUID;

    [SerializeField]
    private bool isPhysInitialized;
    [SerializeField, HideInInspector] private bool isPhysicsConfigured;

    [SerializeField, HideInInspector] private List<Component> ownedComponents = new();

    public T AddOwnedComponent<T>() where T : Component
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(this, "Track graph component");
            var component = Undo.AddComponent<T>(gameObject);
            ownedComponents.Add(component);
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            return component;
        }
#endif
        var runtimeComponent = gameObject.AddComponent<T>();
        ownedComponents.Add(runtimeComponent);
        return runtimeComponent;
    }

    /// <summary>Удаляет только компоненты, созданные графом, сохраняя пользовательские authoring-компоненты.</summary>
    public void RemoveOwnedComponents()
    {
        for (int i = ownedComponents.Count - 1; i >= 0; i--)
        {
            var component = ownedComponents[i];
            if (!component || component.gameObject != gameObject) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.DestroyObjectImmediate(component);
            else Destroy(component);
#else
            Destroy(component);
#endif
        }
    }

    public void AddPhysics()
    {
        if (isPhysInitialized) return;

        isPhysInitialized = true;
        //PhysicsBodyAuthoring body = null;
        PhysicsShapeAuthoring shape = null;
        shape = gameObject.TryGetComponent(out shape) ? shape : AddOwnedComponent<PhysicsShapeAuthoring>();
        //body = gameObject.TryGetComponent(out body) ? body : gameObject.AddComponent<PhysicsBodyAuthoring>();

        shape.BelongsTo = new PhysicsCategoryTags() { Value = (1u << 1) | (1u << 2) };
        shape.CollidesWith = new PhysicsCategoryTags() { Value = (1u << 0) | (1u << 1) | (1u << 2) | (1u << 3) };
    }

    /// <summary>Настраивает существующую форму объекта как зону событий физики для игроков.</summary>
    public void AddTriggerPhysics()
    {
        AddPhysics();


        if (!TryGetComponent(out PhysicsShapeAuthoring shape))
            shape = AddOwnedComponent<PhysicsShapeAuthoring>();

        shape.OverrideBelongsTo = true;
        shape.OverrideCollidesWith = true;
        shape.OverrideCollisionResponse = true;
        shape.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
    }

    public bool ConfigurePhysics(out string error)
    {
        error = null;
        if (TryGetComponent<Rigidbody>(out _))
            error = "IsPhysics requires PhysicsBodyAuthoring, not a GameObject Rigidbody.";
        TryGetComponent<PhysicsShapeAuthoring>(out var shape);
        TryGetComponent<PhysicsBodyAuthoring>(out var body);
        if (body != null && (!body.enabled || body.MotionType == BodyMotionType.Static))
            error = "IsPhysics requires an enabled Dynamic or Kinematic PhysicsBodyAuthoring.";
        if (shape != null && !shape.enabled)
            error = "Enable PhysicsShapeAuthoring on the IsPhysics target.";
        if (error != null) return false;

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            error = "Configure graph physics in Edit Mode.";
            return false;
        }
        if (!isPhysicsConfigured)
        {
            Undo.RecordObject(this, "Enable graph physics");
            isPhysicsConfigured = true;
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
        if (shape == null)
            shape = AddOwnedComponent<PhysicsShapeAuthoring>();

        if (body == null)
        {
            body = AddOwnedComponent<PhysicsBodyAuthoring>();
            body.MotionType = BodyMotionType.Dynamic;
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            EditorUtility.SetDirty(body);
        }
        foreach (var collider in GetComponents<UnityEngine.Collider>())
            Undo.DestroyObjectImmediate(collider);
#endif
        return true;
    }

    public void DisablePhysics()
    {
#if UNITY_EDITOR
        if (!isPhysicsConfigured || Application.isPlaying) return;

        Undo.RecordObject(this, "Disable graph physics");
        foreach (var body in GetComponents<PhysicsBodyAuthoring>())
            Undo.DestroyObjectImmediate(body);

        ownedComponents.RemoveAll(component => component == null);
        isPhysicsConfigured = false;
        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
    }
    public bool TrySetCraftGUID(string guid)
    {
        if (string.IsNullOrEmpty(_ownerGUID))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(this, "Assign CraftObject owner");
#endif
            _ownerGUID = guid;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
            return true;
        }
        else if (_ownerGUID == guid)
            return true;
        return false;
    }
    public void ResetGUID() => _ownerGUID = null;
}
