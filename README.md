# VRCHierarchyHighlighter

ヒエラルキの階層表示をハイライトで強調表示するのと、
VRCで扱う重要コンポーネントをアイコンで表示するやつです。

対応コンポーネントは、
* VRC_AvaterDescriptor(or VRC.SDK3.Components.VRCAvaterDescriptor)
* VRC_MirrorReflection(or VRC.SDK3.Components.VRCMirrorReflection)
* MeshRenderer
* SkinnedMeshRenderer
* DynamicBone (+ Collider)
* VRCPhysBone (+ Collider)
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
2022.04.25.0
    * [VRChat] Physics Bone対応
      * VRCPhysBone, VRCPhysBoneCollider用のアイコンを用意し, 描画するようにしました.

2021.07.28.0
    * Unity2019LTS対応 (2)
      * オブジェクトアクティブ状態のトグルを再実装
        * Unityで標準対応されたトグル機能は表示状態を変えるのみでアクティブ状態を変えるものではなかったため, 再実装しました。標準機能と使い分けてください
        * 新しい方式では各オブジェクトのアイコン部分をクリックすることでアクティブ状態を切り替える事が出来ます（アイコンが無くても大丈夫です）
        * 設定パネル "Enable Object Toggle Checkbox" よりこの機能を有効にするかどうか切り替える事が出来ます
        * 設定パネル "Show Toggle Icons" によりマウスオーバー時のxアイコン表示を切り替える事ができます（"Enable Object Toggle Checkbox"と連動しています）
      * 新しいハイライトモード "Left" の実装
        * Leftを選択すると左側のみハイライトが表示されます. Underモードと同様限られた部分のみハイライトを行うため, 見やすくなります.
        * 複数モードが追加されたため, モード選択を設定パネルのコンボボックスから行えるようにしました.

2021.07.27.0
    * Unity2019LTS正式対応
      * 不具合がある, またprefabのアイコンを消してしまうため, オブジェクトのトグルボタンは廃止しました
        * この機能は2019から標準搭載されましたので, そちらをお使いください. https://docs.unity3d.com/ja/2019.4/Manual/SceneVisibility.html
      * ヒエラルキ左部のアイコン領域を隠してしまわないよう, オブジェクトアイコンを右に移動しました

2020.12.26.0
    * Unity2019ダークモードへの対応
      * Unity2018からUnity2019に移行した際、またその逆の場合はそれぞれデフォルトの設定が自動的に適用されます
      * 上手く適用されない場合は `Window -> VRCHierarchyHighlighter` から設定パネルを表示し、その中の `Default` ボタンを押してください
    * 設定できる色空間設定の範囲の見直しを行いました

2020.11.25.0
    * アンダーラインハイライトモードの実装。オプションから設定出来ます
    * オブジェクトをヒエラルキ上からon/off出来るチェックボックスを実装。オプションから設定出来ます
    * ダークモード対応準備
    * バージョン表記を追加

2020.11.24.0
    ヒエラルキに存在するSceneの行はハイライトしないようにしました。

2020.11.17.0
    SDK3 (Avaters) に含まれる `VRC Avatar Descriptor` に対応していなかったため、対応を行いました。

2020.08.17.0
    SDK3（Avaters, Worlds）ではコンポーネントの名前空間及び名称が変更されているため、それに対応する変更を行いました。
    この変更は応急的なものであり、他の場所で `MirrorRefrection` や `AvaterDescriptor` といったコンポーネントを定義していた場合は影響が及ぶ場合があります。

2018.10.17.0
	マージしたPRの変更点が反映されたバージョンです
	このバージョンには
		* Dynamic Boneコンポーネントの扱いの修正
		* Unityのビルトインアイコンがある場合はそれを使うように修正
	などの対応が含まれます。（詳細はリポジトリのCloseされたPR, #2~#4を参照）
	Thanks for contribution, @esperecyan ! 

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

Copyright(c) 2019-201 AzuriteLab
