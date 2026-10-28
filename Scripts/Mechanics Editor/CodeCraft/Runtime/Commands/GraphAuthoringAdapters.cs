using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HSE.CodeCraft
{
    internal static class GraphAuthoringAdapters
    {
        public static bool Process(BaseGraph graph, GraphBaker baker)
        {
            var changes = new List<AuthoringAdapterNode>();
            var adapters = new List<AuthoringAdapterNode>();
            var owners = new HashSet<(GameObject Target, Type Authoring)>();
            bool valid = true;
            foreach (var node in graph.nodes)
            {
                if (!(node is AuthoringAdapterNode adapter) || !node.IsConnected) continue;
                if (!adapter.ValidateAdapter())
                {
                    valid = false;
                    continue;
                }
                if (graph.linkedSceneObject != null && adapter.TargetObject.scene != graph.linkedSceneObject.gameObject.scene)
                    valid &= Fail(adapter, "Authoring target must belong to the scenario scene.");
                if (!owners.Add((adapter.TargetObject, adapter.AuthoringType)))
                    valid &= Fail(adapter, "Multiple graph nodes configure the same authoring.");

                adapter.RegisterDependencies(baker?.Baker);
                adapters.Add(adapter);
                if (adapter.IsApplied) continue;
                if (baker != null)
                    valid &= Fail(adapter, "Authoring settings are not applied. Run Show Processor / Play and save the scene before baking.");
                changes.Add(adapter);
            }
            if (!valid) return false;
            if (baker != null)
            {
                foreach (var adapter in adapters) adapter.CraftCommands(baker);
                return true;
            }
            if (changes.Count == 0) return true;

#if UNITY_EDITOR
            if (Application.isPlaying || BuildPipeline.isBuildingPlayer || AssetDatabase.IsAssetImportWorkerProcess())
                return Fail(changes[0], "Apply authoring settings in edit mode before building.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply graph authorings");
            try
            {
                foreach (var adapter in changes) adapter.Apply();
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                return Fail(changes[0], exception.GetBaseException().Message);
            }
            return true;
#else
            return Fail(changes[0], "Authoring adapters cannot modify runtime objects.");
#endif
        }

        private static bool Fail(AuthoringAdapterNode node, string message)
        {
            node.AddMessage(message, NodeMessageType.Error);
            return false;
        }
    }
}
