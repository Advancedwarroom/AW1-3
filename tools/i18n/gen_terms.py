#!/usr/bin/env python3
"""从 tools/i18n/terms.tsv 生成 spricts/TranslateGroup/TranslateGroup.Terms.cs
列：zh  en  ja  ko  de  ru（繁中由 OpenCC s2twp 自动生成）
同时生成简→繁单字表（覆盖 spricts 下所有出现过的汉字），作为繁中兜底。
用法：pip install opencc-python-reimplemented && python3 tools/i18n/gen_terms.py
"""
import os, re, glob
from opencc import OpenCC
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
tw = OpenCC('s2twp'); t1 = OpenCC('s2t')
def esc(s): return s.replace('\\', '\\\\').replace('"', '\\"')
rows = []
for ln in open(os.path.join(root, 'tools/i18n/terms.tsv'), encoding='utf-8'):
    ln = ln.rstrip('\n')
    if not ln or ln.startswith('#'): continue
    p = ln.split('\t'); assert len(p) == 6, ln
    rows.append(p)
chars = set()
for f in glob.glob(os.path.join(root, 'spricts/**/*.cs'), recursive=True):
    chars |= set(re.findall(r'[\u4e00-\u9fff]', open(f, encoding='utf-8').read()))
pairs = sorted((c, t1.convert(c)) for c in chars)
pairs = [(a, b) for a, b in pairs if a != b and len(b) == 1]
out = ['// 自动生成：tools/i18n/gen_terms.py ← tools/i18n/terms.tsv，请勿手改',
       'using System.Collections.Generic;', '',
       'public partial class TranslateGroup', '{',
       '    // 词条（仅在 Dictionary.cs 未定义该键时加入，手写词条优先）',
       '    private void AddGeneratedTerms()', '    {',
       '        void T6(string k, string en, string ja, string ko, string de, string ru, string tw)',
       '        {',
       '            if (translations.ContainsKey(k)) return;',
       '            translations[k] = new Dictionary<GameLanguage, string>',
       '            {',
       '                [GameLanguage.English] = en, [GameLanguage.Japanese] = ja, [GameLanguage.Korean] = ko,',
       '                [GameLanguage.German] = de, [GameLanguage.Russian] = ru,',
       '                [GameLanguage.SimplifiedChinese] = k, [GameLanguage.TraditionalChinese] = tw',
       '            };',
       '        }']
for zh, en, ja, ko, de, ru in rows:
    out.append(f'        T6("{esc(zh)}", "{esc(en)}", "{esc(ja)}", "{esc(ko)}", "{esc(de)}", "{esc(ru)}", "{esc(tw.convert(zh))}");')
out += ['    }', '',
        '    // 简→繁单字兜底表',
        '    private const string TradMapFrom = "' + ''.join(a for a, _ in pairs) + '";',
        '    private const string TradMapTo   = "' + ''.join(b for _, b in pairs) + '";',
        '}', '']
open(os.path.join(root, 'spricts/TranslateGroup/TranslateGroup.Terms.cs'), 'w', encoding='utf-8').write('\n'.join(out))
print(len(rows), 'terms,', len(pairs), 'trad chars')
