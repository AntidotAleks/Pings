using System;
using System.Collections.Generic;
using System.Linq;
using I2.Loc;
using UnityEngine;

namespace pings
{
    public static class TranslationCheck
    {
        private static LanguageSourceData _lang;

        public static void TermTreeList()
        {
            _lang = LocalizationManager.Sources[0];
            

            var hash = new HashSet<string>();
            foreach (var term in from term in _lang.GetTermsList() where !term.StartsWith("ModPings/") let pos = term.IndexOf("/", StringComparison.Ordinal) where pos >= 0 select term)
                hash.Add(term.Substring(0, term.LastIndexOf("/", StringComparison.Ordinal)));
            
            Debug.Log($"Terms: {_lang.GetTermsList().Count} | Hashes: {hash.Count}");
            Debug.Log(hash.OrderBy(term => term).Aggregate("", (current, term) => current + $"{term}, ").TrimEnd(',', ' '));
            
            
        }
        public static void TermSearch(string arg)
        {
            _lang = LocalizationManager.Sources[0];
            
            var uniqueTerms = new HashSet<string>();
            foreach (var term in from term in _lang.GetTermsList() let pos = term.IndexOf("/", StringComparison.Ordinal) where pos >= 0 where term.Contains(arg) select term)
                uniqueTerms.Add(term);
            
            Debug.Log($"Terms found: {uniqueTerms.Count}");
            Debug.Log(uniqueTerms.OrderBy(term => term).Aggregate("", (current, term) => current + $"{term}, ").TrimEnd(',', ' '));
        }
        
        public static void Translate(string arg)
        {
            var translate = _lang.GetTermsList().First(term => term == arg);
            Debug.Log(arg + " = " + _lang.GetTranslation(translate));
        }
    }
}