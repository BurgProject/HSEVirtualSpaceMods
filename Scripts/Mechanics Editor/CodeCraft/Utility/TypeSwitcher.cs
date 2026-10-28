using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSE.CodeCraft
{
    public class TypeSwitcher
    {
        Dictionary<Type, Action<object>> matches;
        Dictionary<Type, Action> matchesNonAct;

        public TypeSwitcher(bool withAction = false) {
            if (withAction)
                matches = new Dictionary<Type, Action<object>>();
            else matchesNonAct = new Dictionary<Type, Action>();
        }
        public TypeSwitcher Case<T>(Action<T> action) { 

            matches?.Add(typeof(T), (x) => action((T)x)); 
            return this; 
        }
        public TypeSwitcher Case<T>(Action action = null, Action isArrayAction = null)
        {
            if (action != null){
                matchesNonAct?.Add(typeof(T), () => action());
            }
            if (isArrayAction != null)
                matchesNonAct?.Add(typeof(T[]), () => isArrayAction());

            return this;
        }
        public void Switch(object x) => matches?[x.GetType()](x);
        public bool Switch(Type x)
        {
            Action act = null;
            
            bool? existType = matchesNonAct?.TryGetValue(x, out act);
            if(!existType.Value)
            {
                
                foreach (var interfac in x.GetInterfaces()){
                    existType= matchesNonAct.TryGetValue(interfac, out act);
                    if (existType.Value)
                        break;
                }
                if (!existType.Value){
                    Type baseType = x.BaseType;
                    int layerCount = 0;
                    while (layerCount < 5)
                    {
                        if (baseType == null || matchesNonAct.TryGetValue(baseType, out act))
                            break;
                        baseType = baseType.BaseType;
                        layerCount++;
                    }
                }
            }
            if(act != null)
            { 
                act(); 
                return true;
            } else return false;
        }
    }
}
