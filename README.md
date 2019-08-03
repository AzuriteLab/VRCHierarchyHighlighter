# VRCHierarchyHighlighter

ヒエラルキの階層表示をハイライトで強調表示するのと、
VRCで扱う重要コンポーネントをアイコンで表示するやつです。

対応コンポーネントは、
* VRC_AvaterDescriptor
* VRC_MirrorReflection
* MeshRenderer
* SkinnedMeshRenderer
* DynamicBone
* AudioSource
* Light
* LightProbeGroup
* ReflectionProbe
です。
（今後Rigidbody, Joint, Clothなどに対応するかもしれません）

このエディタ拡張を導入することによって、
間違った階層にオブジェクトを突っ込むリスクを下げたり、オブジェクトにちゃんとDynamicBoneが適用されているのか、
どのオブジェクトがVRCのアバターコンポーネントを持っているのか、メッシュ構造を持っているのかを可視化することが出来ます。

## 使い方

BOOTHまたはgithubのリリースページからダウンロードしたunitypackageをプロジェクトのAssetsフォルダ直下に突っ込んでください（それ以外だと動作しません）

設定パネルはUnityのメニューバーから `Window -> VRCHierarchyHighlighter` を選択して表示できます。

## 捕捉

このプロジェクトはMIT Licenseとなっているため、改造して再配布など自由です。
より見やすいアイコンだったり、表示方法があれば勝手にやってもらって構いません。
ただし、ライセンスに準拠し保持者であるわたしの名前を見える場所に表記する必要があります。

## 注意

コンポーネント名を文字列情報でマッチさせているため、
オブジェクト名に明示的にコンポーネント名を含めていた場合は誤って判定されるケースがあります。

## 更新履歴

```
2019.08.03.0
	修正
	* DynamicBoneColliderが含まれていた場合に以降のオブジェクトが描画されなくなる不具合を修正

2019.08.02.0
    機能を追加
    * 新たなコンポーネント対応
        * AudioSource, LightProbeGroup, ReflectionProbe, Light, VRC_MirrorReflection
    修正
    * 既存のアイコンを修正
        * DynamicBone, MeshRenderer, SkinnedMeshRenderer
    * コンポーネントの判定を名前ベースではなく型ベースに変更

2019.07.25.0
    機能を追加
    * コントロールパネルの実装（Window -> VRCHierarchyHighlighter）
        * ハイライトの色をHSVで変更できるようにしました
        * アイコン/ハイライト/頂点数カウント（新機能）のon/offをできるようにしました
    * SkinnedMeshRendererの頂点数をヒエラルキから確認できるようにしました（デフォルトoff）
    * DynamicBoneに有効なターゲットが指定されていない場合、アイコンが薄くなるようにしました
    * MeshRendererのアイコンを表示するようにしました

2019.05.19.0
	バグを修正
	実行モードから戻ると何故かメンバが初期化され、アイコンリソースが解放されてしまう問題に対処
	
2019.04.27.0
	重篤なバグを修正
	例えば、既にDynamicBoneが設定されているアバターのprefabをHierarchyに読み込んだ場合、
	DynamicBoneが存在しない場合は該当オブジェクトがnullとなってしまい、VRCHierarchyHighlighterが
	メモリアクセスエラーによってエラーで中断されてしまい、以降のGameObjectが描画されない問題がありました
```

Copyright(c) 2019 AzuriteLab
