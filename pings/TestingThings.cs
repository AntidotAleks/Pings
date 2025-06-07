using System;
using System.Collections.Generic;
using System.Linq;
using I2.Loc;
using UnityEngine;

namespace pings
{
    public static class TestingThings
    {
        private static LanguageSourceData _lang;

        public static void G()
        {
            _lang = LocalizationManager.Sources[0];
            

            var hash = new HashSet<string>();
            foreach (var term in from term in _lang.GetTermsList() let pos = term.IndexOf("/", StringComparison.Ordinal) where pos >= 0 select term)
                hash.Add(term.Substring(0, term.LastIndexOf("/", StringComparison.Ordinal)));
            
            Debug.Log($"Terms: {_lang.GetTermsList().Count} | Hashes: {hash.Count}");
            Debug.Log(hash.OrderBy(term => term).Aggregate("", (current, term) => current + $"{term}, ").TrimEnd(',', ' '));
            
            
        }
        public static void G(string arg)
        {
            var uniqueTerms = new HashSet<string>();
            foreach (var term in from term in _lang.GetTermsList() let pos = term.IndexOf("/", StringComparison.Ordinal) where pos >= 0 where term.Contains(arg) select term)
                uniqueTerms.Add(term);
            
            Debug.Log($"Unique Terms: {uniqueTerms.Count}");
            Debug.Log(uniqueTerms.OrderBy(term => term).Aggregate("", (current, term) => current + $"{term}, ").TrimEnd(',', ' '));
        }
        
        public static void D(string arg)
        {
            var translate = _lang.GetTermsList().First(term => term == arg);
            Debug.Log(arg + " = " + _lang.GetTranslation(translate));
        }
        
        public static void E(Transform transform)
        {
            if (!transform) return;
            
            Debug.Log("[Pings: Handling] Test");
            var index = 0;
            do
            {
                var components = transform.GetComponents<Component>();
                var str = components.Aggregate(index + ": ", (current, component) => current + $"{component.GetType().Name}{(component is MonoBehaviour?"<-script":"")}, ");
                str = "["+str.TrimEnd(',', ' ')+"]";
                Debug.Log($"> {str} for {transform.name}");
                
                transform = transform.parent;
                index++;
            } while (transform);
        }
    }
}