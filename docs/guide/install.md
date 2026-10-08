# 入れ方

## 動かす環境

Unity 2022.3 と、VRChat Worlds SDK 3.10.5 以降で動きます。

## VCC から入れる

<div class="steps">

1. [vpm.haru0416.dev](https://vpm.haru0416.dev) を開き、「VCC に追加」を押します。ボタンで開かないときは、VCC の <span class="menu">Settings → Packages → Add Repository</span> に `https://vpm.haru0416.dev/index.json` を入れます
2. VCC でワールドのプロジェクトの <span class="menu">Manage Project</span> を開きます
3. 一覧の Tripwire の ＋ を押して入れます。Tripwire が使う Udon Bridge も一緒に入ります

</div>

ALCOM でも同じ手順で入れられます。

::: tip 入ったか確かめる
Unity でオブジェクトを選び、<span class="menu">Add Component → Tripwire → Tripwire Trigger</span> が出れば入っています。
:::

## シーンの準備

Play で動きを確かめるには、シーンに VRCWorld（VRChat のシーン設定）が必要です。ないときは、Tripwire Trigger の Inspector に「VRCWorld を置く」ボタンが出ます。Play を押すと、VRChat SDK の ClientSim が自分のプレイヤーを出し、クリックや範囲への出入りを試せます。

## サンプルを入れる

マニュアルのレシピをシーンにまとめたサンプルがあります。<span class="menu">Window → Package Manager</span> で Tripwire を選び、Samples の Recipes の <span class="menu">Import</span> を押すと、`Assets/Samples/Tripwire` に入ります。

## 更新と削除

更新は VCC で新しい版を選ぶだけです。トリガーのデータは古い版で保存したものもそのまま読めます。新しい版で保存したトリガーは、古い版の Tripwire では編集できません（Inspector がそう知らせます）。

Tripwire を外すときは、先にすべてのトリガーを反映しておきます。反映済みのギミックは生成された UdonSharp だけで動くので、Tripwire を外しても動き続けます（Tripwire Trigger のコンポーネントは Missing Script になるので消してください）。
