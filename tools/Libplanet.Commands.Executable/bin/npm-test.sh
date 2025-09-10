#!/bin/bash
set -e

# Taken from https://github.com/har7an/bash-semver-regex
# Regex for a semver digit
D='0|[1-9][0-9]*'
# Regex for a semver pre-release word
PW='[0-9]*[a-zA-Z-][0-9a-zA-Z-]*'
# Regex for a semver build-metadata word
MW='[0-9a-zA-Z-]+'

if [[ "$1" = "" ]]; then
  echo error: missing version to download >&2
  exit 1
elif [[ "$1" =~ ^($D)\.($D)\.($D)(-(($D|$PW)(\.($D|$PW))*))?(\+($MW(\.$MW)*))?$ ]]; then
  version="$1"
else
  echo error: invalid version number: "$1" >&2
  exit 1
fi

function test_npx() {
  cmd="$1"
  expected="$2"
  actual="$(npx $1)"
  if [[ "$actual" != "$expected" ]]; then
    {
      echo "The command \`$cmd' printed an unexpected output."
      echo "  expected: $expected"
      echo "  actual:   $actual"
    } >&2
    exit 1
  fi
}

function test_planet() {
  test_npx "planet --version" "planet $version"
}

pkgdir="$(cd "$(dirname "$0")"; cd ..; pwd)"

cp "$pkgdir/package.json" "$pkgdir/.package.json.bak"
# shellcheck disable=SC2064
trap "mv '$pkgdir/.package.json.bak' '$pkgdir/package.json'" EXIT
jq --arg v "$version" 'del(.private) | .version = $v' package.json \
  > .package.json.tmp
mv .package.json.tmp package.json

rm -f package.tgz
yarn pack --install-if-needed

echo Test with \`npm install\'... >&2
pushd "$(mktemp -d)"
npm install --quiet --save "$pkgdir/package.tgz"
test_planet
popd

echo Test with \`npm install --ignore-scripts\'... >&2
pushd "$(mktemp -d)"
npm install \
  --quiet \
  --ignore-scripts \
  --save \
  "$pkgdir/package.tgz"
test_planet
popd

rm "$pkgdir"/package.tgz

echo "Succeeded!"
