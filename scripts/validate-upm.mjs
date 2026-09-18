#!/usr/bin/env node
/**
 * Offline UPM package structure checks (no Unity required).
 * Exit 0 on success; print failures and exit 1 otherwise.
 */
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const errors = [];

function fail(message) {
  errors.push(message);
}

function readJson(path) {
  try {
    return JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    fail(`${relative(repoRoot, path)}: ${error.message}`);
    return null;
  }
}

function requireFile(path, label = path) {
  if (!existsSync(path)) fail(`missing file: ${label}`);
  return existsSync(path);
}

function requireDir(path, label = path) {
  if (!existsSync(path) || !statSync(path).isDirectory()) fail(`missing directory: ${label}`);
  return existsSync(path) && statSync(path).isDirectory();
}

// UPM package.json deps: SemVer only (git/file refs belong in project manifest.json).
// Third-party packages not on the Unity registry MUST NOT be listed — they break
// "Add package from git URL" (UPM looks up SemVer on configured registries only).
// Those are peer deps provided by the consuming project / Starter manifests.
const SEMVER = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$/;
const ALLOWED_DEP = /^(com\.unity\.|com\.source27\.cascade)/;
function requirePackageDeps(pkgJsonPath, label) {
  const pkg = readJson(pkgJsonPath);
  if (!pkg) return;
  const deps = pkg.dependencies;
  if (deps && typeof deps === 'object') {
    for (const [dep, value] of Object.entries(deps)) {
      if (typeof value !== 'string' || !SEMVER.test(value)) {
        fail(`${label} dependency ${dep}: "${value}" is not SemVer (git/file refs only work in a project manifest.json)`);
      }
      if (!ALLOWED_DEP.test(dep)) {
        fail(`${label} dependency ${dep}: not Unity-registry/com.source27.cascade* (declare as project peer dep instead)`);
      }
    }
  }
}

// --- root layout ---
if (existsSync(join(repoRoot, 'package.json'))) {
  fail('package.json must NOT live at repo root (use Cascade/package.json + ?path=Cascade)');
}
requireFile(join(repoRoot, 'LICENSE'), 'LICENSE');
requireFile(join(repoRoot, 'README.md'), 'README.md');
requireFile(join(repoRoot, 'CONTEXT.md'), 'CONTEXT.md');

const packageJsonPath = join(repoRoot, 'Cascade', 'package.json');
requireFile(packageJsonPath, 'Cascade/package.json');
requirePackageDeps(packageJsonPath, 'Cascade');
const pkg = readJson(packageJsonPath);
if (pkg) {
  if (pkg.name !== 'com.source27.cascade') fail(`Cascade package name: expected com.source27.cascade, got ${pkg.name}`);
  if (!pkg.version) fail('Cascade/package.json missing version');
  if (!pkg.unity) fail('Cascade/package.json missing unity');
  if (pkg.license !== 'MIT') fail(`Cascade/package.json license: expected MIT, got ${pkg.license}`);
  if (!pkg.dependencies || typeof pkg.dependencies !== 'object') fail('Cascade/package.json missing dependencies');
  for (const dep of Object.keys(pkg.dependencies)) {
    if (/hybridclr|lit-motion|loopscroll|unitask|yooasset|cysharp|ugui|textmeshpro|2d\.sprite/i.test(dep)) {
      fail(`Cascade must not depend on ${dep} (UI + atlas live in Modules/UI, localization in Modules/Localization)`);
    }
  }
}

const requiredAsmdefs = [
  'Cascade/Runtime/Cascade.Bootstrap/Cascade.Bootstrap.asmdef',
  'Cascade/Runtime/Cascade.Service/Cascade.Service.asmdef',
  'Cascade/Runtime/Cascade.Core/Cascade.Core.asmdef',
  'Cascade/Editor/Cascade.Editor/Cascade.Editor.asmdef',
  'Cascade/Tests/Cascade.Tests/Cascade.Tests.asmdef',
  'Modules/UI/Runtime/Cascade.Modules.UI.asmdef',
  'Modules/UI/Editor/Cascade.Modules.UI.Editor.asmdef',
  'Modules/Localization/Runtime/Cascade.Modules.Localization.asmdef',
  'Modules/Localization/Editor/Cascade.Modules.Localization.Editor.asmdef',
];
for (const rel of requiredAsmdefs) requireFile(join(repoRoot, rel), rel);

requireDir(join(repoRoot, 'Modules', 'UI', 'Roslyn'), 'Modules/UI/Roslyn');
const roslynDlls = existsSync(join(repoRoot, 'Modules', 'UI', 'Roslyn'))
  ? readdirSync(join(repoRoot, 'Modules', 'UI', 'Roslyn')).filter((name) => name.endsWith('.dll'))
  : [];
if (roslynDlls.length === 0) fail('Modules/UI/Roslyn must contain at least one source-generator .dll');

requireFile(
  join(repoRoot, 'Modules', 'UI', 'Tools~', 'Cascade.SourceGenerator', 'Cascade.SourceGenerator.csproj'),
  'Modules/UI/Tools~/Cascade.SourceGenerator/Cascade.SourceGenerator.csproj',
);

const yooPath = join(repoRoot, 'Integrations', 'YooAsset', 'package.json');
requireFile(yooPath, 'Integrations/YooAsset/package.json');
requirePackageDeps(yooPath, 'Integrations/YooAsset');
const yoo = readJson(yooPath);
if (yoo) {
  if (yoo.name !== 'com.source27.cascade.integrations.yooasset') {
    fail(`YooAsset package name mismatch: ${yoo.name}`);
  }
  if (yoo.license !== 'MIT') fail(`Integrations/YooAsset/package.json license: expected MIT, got ${yoo.license}`);
  requireFile(
    join(repoRoot, 'Integrations', 'YooAsset', 'Runtime', 'Cascade.Service.YooAsset.asmdef'),
    'Integrations/YooAsset/Runtime/Cascade.Service.YooAsset.asmdef',
  );
}

const locPath = join(repoRoot, 'Modules', 'Localization', 'package.json');
requireFile(locPath, 'Modules/Localization/package.json');
requirePackageDeps(locPath, 'Modules/Localization');
const loc = readJson(locPath);
if (loc) {
  if (loc.name !== 'com.source27.cascade.modules.localization') {
    fail(`Localization package name mismatch: ${loc.name}`);
  }
  if (loc.license !== 'MIT') {
    fail(`Modules/Localization/package.json license: expected MIT, got ${loc.license}`);
  }
}

const audioPath = join(repoRoot, 'Modules', 'Audio', 'package.json');
requireFile(audioPath, 'Modules/Audio/package.json');
requirePackageDeps(audioPath, 'Modules/Audio');
const audio = readJson(audioPath);
if (audio) {
  if (audio.name !== 'com.source27.cascade.modules.audio') {
    fail(`Audio package name mismatch: ${audio.name}`);
  }
  if (audio.license !== 'MIT') {
    fail(`Modules/Audio/package.json license: expected MIT, got ${audio.license}`);
  }
}

const uiPath = join(repoRoot, 'Modules', 'UI', 'package.json');
requireFile(uiPath, 'Modules/UI/package.json');
requirePackageDeps(uiPath, 'Modules/UI');
const ui = readJson(uiPath);
if (ui) {
  if (ui.name !== 'com.source27.cascade.modules.ui') {
    fail(`UI package name mismatch: ${ui.name}`);
  }
  if (ui.license !== 'MIT') {
    fail(`Modules/UI/package.json license: expected MIT, got ${ui.license}`);
  }
}

// --- starters ---
requireFile(
  join(repoRoot, 'Starters', 'Mobile', 'Packages', 'manifest.json'),
  'Starters/Mobile/Packages/manifest.json',
);
requireFile(
  join(repoRoot, 'Starters', 'Mobile', 'ProjectSettings', 'ProjectVersion.txt'),
  'Starters/Mobile/ProjectSettings/ProjectVersion.txt',
);
requireFile(
  join(repoRoot, 'Starters', 'Indie', 'Packages', 'manifest.json'),
  'Starters/Indie/Packages/manifest.json',
);
requireFile(
  join(repoRoot, 'Starters', 'Indie', 'ProjectSettings', 'ProjectVersion.txt'),
  'Starters/Indie/ProjectSettings/ProjectVersion.txt',
);
const projectVersion = readFileSync(
  join(repoRoot, 'Starters', 'Mobile', 'ProjectSettings', 'ProjectVersion.txt'),
  'utf8',
);
if (!/m_EditorVersion:\s*2022\.3\.62f3/.test(projectVersion)) {
  fail('Starters/Mobile ProjectVersion.txt must pin Unity 2022.3.62f3');
}
const indieVersion = readFileSync(
  join(repoRoot, 'Starters', 'Indie', 'ProjectSettings', 'ProjectVersion.txt'),
  'utf8',
);
if (!/m_EditorVersion:\s*2022\.3\.62f3/.test(indieVersion)) {
  fail('Starters/Indie ProjectVersion.txt must pin Unity 2022.3.62f3');
}
requirePackageDeps(join(repoRoot, 'Integrations', 'Addressables', 'package.json'), 'Integrations/Addressables');
requirePackageDeps(join(repoRoot, 'Modules', 'UiExtras', 'package.json'), 'Modules/UiExtras');

// --- starters must track the current module names/paths ---
const starterManifests = {
  'Starters/Mobile': join(repoRoot, 'Starters', 'Mobile', 'Packages', 'manifest.json'),
  'Starters/Indie': join(repoRoot, 'Starters', 'Indie', 'Packages', 'manifest.json'),
};
for (const [label, path] of Object.entries(starterManifests)) {
  const manifest = readJson(path);
  if (!manifest || !manifest.dependencies) continue;
  for (const dep of Object.keys(manifest.dependencies)) {
    if (/localizationtools/i.test(dep)) {
      fail(`${label} manifest still depends on ${dep}; use com.source27.cascade.modules.localization`);
    }
    if (dep.startsWith('com.source27.cascade.modules.') && !manifest.dependencies[dep]) {
      fail(`${label} manifest has an empty module dependency: ${dep}`);
    }
  }
  if (!manifest.dependencies['com.source27.cascade.modules.localization']) {
    fail(`${label} manifest must depend on com.source27.cascade.modules.localization`);
  }
  if (label === 'Starters/Mobile' && !manifest.dependencies['com.source27.cascade.modules.ui']) {
    fail(`${label} manifest must depend on com.source27.cascade.modules.ui (hot-update UI stack)`);
  }
  if (label === 'Starters/Indie' && manifest.dependencies['com.source27.cascade.modules.ui']) {
    fail(`${label} manifest must NOT depend on com.source27.cascade.modules.ui (keeps the module optional)`);
  }
  // The InputGlyphs integration cannot compile without its peer; installing one half breaks the project.
  if (manifest.dependencies['com.source27.cascade.integrations.inputglyphs'] &&
      !manifest.dependencies['com.eviltwo.input-glyphs']) {
    fail(`${label} installs com.source27.cascade.integrations.inputglyphs without its peer com.eviltwo.input-glyphs`);
  }
}

// --- optional-package catalogue must mirror the repo layout ---
const cataloguePath = join(repoRoot, 'Cascade', 'Editor', 'Cascade.Editor', 'PackageCatalogue.json');
requireFile(cataloguePath, 'Cascade/Editor/Cascade.Editor/PackageCatalogue.json');
const catalogue = readJson(cataloguePath);
if (catalogue) {
  const entries = catalogue.packages ?? [];
  const listed = new Map(entries.map((entry) => [entry.path, entry]));
  const actual = [];
  for (const root of ['Modules', 'Integrations']) {
    const dir = join(repoRoot, root);
    if (!existsSync(dir)) continue;
    for (const name of readdirSync(dir)) {
      const full = join(dir, name);
      if (statSync(full).isDirectory() && existsSync(join(full, 'package.json'))) actual.push(`${root}/${name}`);
    }
  }
  for (const dir of actual) {
    if (!listed.has(dir)) fail(`PackageCatalogue.json is missing ${dir} (Cascade 集成与模块 window would not list it)`);
  }
  for (const [dir, entry] of listed) {
    if (!actual.includes(dir)) fail(`PackageCatalogue.json lists ${dir}, but no package.json lives there`);
    for (const field of ['name', 'displayName', 'kind', 'path', 'when']) {
      if (!entry[field]) fail(`PackageCatalogue.json entry ${dir} is missing "${field}"`);
    }
    const pkg = readJson(join(repoRoot, dir, 'package.json'));
    if (pkg && pkg.name !== entry.name) {
      fail(`PackageCatalogue.json entry ${dir} name mismatch: ${entry.name} vs ${pkg.name}`);
    }
  }
}

// --- every Cascade module/integration assembly a starter references must be in its manifest ---
const assemblyToPackage = {};
for (const root of ['Modules', 'Integrations']) {
  const dir = join(repoRoot, root);
  if (!existsSync(dir)) continue;
  for (const name of readdirSync(dir)) {
    const packageJson = join(dir, name, 'package.json');
    if (!existsSync(packageJson)) continue;
    const pkg = readJson(packageJson);
    if (!pkg) continue;
    const stack = [join(dir, name)];
    while (stack.length > 0) {
      const current = stack.pop();
      for (const entry of readdirSync(current, { withFileTypes: true })) {
        if (entry.name.endsWith('~')) continue;
        const full = join(current, entry.name);
        if (entry.isDirectory()) stack.push(full);
        else if (entry.name.endsWith('.asmdef')) {
          const asmdef = readJson(full);
          if (asmdef && asmdef.name) assemblyToPackage[asmdef.name] = pkg.name;
        }
      }
    }
  }
}
for (const [label, path] of Object.entries(starterManifests)) {
  const manifest = readJson(path);
  if (!manifest || !manifest.dependencies) continue;
  const stack = [join(repoRoot, label, 'Assets')];
  while (stack.length > 0) {
    const current = stack.pop();
    if (!existsSync(current)) continue;
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const full = join(current, entry.name);
      if (entry.isDirectory()) stack.push(full);
      else if (entry.name.endsWith('.asmdef')) {
        const asmdef = readJson(full);
        for (const reference of asmdef?.references ?? []) {
          const pkgName = assemblyToPackage[reference];
          if (pkgName && !manifest.dependencies[pkgName]) {
            fail(`${label} references ${reference} (${pkgName}) but Packages/manifest.json does not depend on it`);
          }
        }
      }
    }
  }
}

if (errors.length > 0) {
  console.error('validate-upm failed:');
  for (const error of errors) console.error(`  - ${error}`);
  process.exit(1);
}

console.log('validate-upm OK');
console.log(`  package ${pkg?.name}@${pkg?.version} (unity ${pkg?.unity})`);
console.log(`  roslyn dlls (Modules/UI/Roslyn): ${roslynDlls.join(', ')}`);
console.log(`  starters editor: 2022.3.62f3`);
