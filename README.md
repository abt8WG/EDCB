EpgDataCap_Bon
==============
**BonDriver based multifunctional EPG software**

Documents are stored in the 'Document' directory.  
Configuration files are stored in the 'ini' directory.

**このフォークについて**

このフォークはxtne6fさんのフォーク([xtne6f@work-plus-s](https://github.com/xtne6f/EDCB/tree/work-plus-s))にちょびっとだけパッチを追加するブランチ(フォーク)です。機能に関する説明やビルド方法などはフォーク元の[xtne6f@work-plus-s](https://github.com/xtne6f/EDCB/tree/work-plus-s)を参照してください。フォーク元との関係は[xtne6f@NetworkGraph](https://github.com/xtne6f/EDCB/network)で確認することが出来ます。

なお、このフォークでは主にEpgTimerへの変更を行っていますが、あちこちコードを改変している上、かなりデンジャラスなコミット([ff60480](https://github.com/tkntrec/EDCB/commit/ff6048074a4a609fb22c78361682a3cb4cf4a593)とか)も混ざっていますので、使用の際はご注意を。

参考【[各画面キャプチャ](https://tkntrec.github.io/EDCB_PrtSc)】

**主な変更点について**

* 検索(自動予約登録)と右クリックメニューまわりを中心に若干の機能追加・変更をしています。
* 「EPG予約条件」ウィンドウ関係
  * 「自動予約登録を削除」「予約全削除」「前へ」「次へ」ボタン追加。
  * 検索結果一覧の右クリックに「番組名で再検索(別ウィンドウ)」を追加。
* 右クリックメニュー関係
  * 右クリックメニュー項目の表示/非表示を選択出来るようにした。  
[設定]-[動作設定]-[その他]の[右クリックメニューの設定]から変更出来ます。  
ショートカットキーを変更したい場合は、設定ファイル(XML)で直接指定してください。
* その他
  * 各設定ウィンドウをESCで閉じられるようにした。
  * 自動予約登録に合わせて予約も変更するオプションを追加した。
  * 予約などの情報を簡易に検索できるダイアログを追加した。
  * 追加設定の説明などを含むコミット  
設定画面ではなく、設定ファイル(XML)で直接指定するオプションについての説明です。  
[1c22086](https://github.com/tkntrec/EDCB/commit/1c220862bc75b84465d1c524227dbac1c8ee3e3b) 各設定画面でのフォルダ選択時に「ファイル選択ダイアログ」を使用するオプション  
[52c598b](https://github.com/tkntrec/EDCB/commit/52c598b17a660fdbe090fcea7c937b3acfc464d8) 録画結果のドロップにカウントしないPIDを設定するオプション  

**ブランチついて**

このフォークのブランチは再作成(リベース)などで構成が変わることがあります。  
[branch:my-build-s](https://github.com/tkntrec/EDCB/tree/my-build-s)以外、特にビルドする意味はありません。  
[branch:my-ui-s](https://github.com/tkntrec/EDCB/tree/my-ui-s)はフォーク元[xtne6f@work-plus-s](https://github.com/xtne6f/EDCB/tree/work-plus-s)とのEpgTimer側の差分、[branch:my-work-s](https://github.com/tkntrec/EDCB/tree/my-work-s6)はEpgTimerSrv側の差分です。
