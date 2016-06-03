EpgDataCap_Bon
==============
**BonDriver based multifunctional EPG software**

Documents are stored in the 'Document' directory.  
Configuration files are stored in the 'ini' directory.

**このForkについて**

master ブランチは
* xtne6f氏 (https://github.com/xtne6f/EDCB) の work-plus-s ブランチ
* tkntrec氏 (https://github.com/tkntrec/EDCB) の my-build-s ブランチ
* nekopanda氏 (https://github.com/nekopanda/EDCB) の develop ブランチ
* EMWUI氏 EDCB_Material_WebUI (https://github.com/EMWUI/EDCB_Material_WebUI) の master ブランチ

を merge しまくりつつ、各氏のサブブランチも先取りしつつ、  
自分の好みも混ぜ合わせてみるキメラのようなブランチです。

なるべく機能削除はしないマージをしていきたいところですが、  
機能削除による削除なのか  
修正による削除なのか  
コンフリクトによる削除なのか  
一つ一つ確認するのが難しい状況です。

README関連のファイルはコンフリクトしやすいので、あまり修正していません。  
各氏のREADMEを参照してください。
* https://github.com/xtne6f/EDCB/tree/work-plus-s/Document
* https://github.com/tkntrec/EDCB/tree/my-build-s/Document
* https://github.com/nekopanda/EDCB/tree/develop/Document
* https://github.com/EMWUI/EDCB_Material_WebUI/blob/master/README.md

**以下、abt8WG版にて追加/変更を行った主なリスト**
* EpgTimerNW を EpgTimer に統合  
  EpgTimerNW にリネームしなくても、ネットワーク接続が可能になりました。  
  サーバー側の設定ファイル(EpgTimerSrv.ini Common.ini など)を参照しなくなり、  
  クライアント側は EpgTimer.exe と EpgTimer.exe.xml のみで運用可能になりました。  

* ネットワーク接続時のパスワード認証の追加  
  変なパケットを受信してEpgTimerSrvが落ちる可能性を減らします。  
  通信の暗号化はしていません。

* EDCBSupport.tvtp ver 0.2.0 をポーティング、認証対応化  
  同じパスワードを指定しないとTvTestから予約等ができなくなります。  

* 予定チューナーでのポップアップ表示  
  tkntrec版にも採用され整理されたので、逆採用させていただきました。  
  1分当たりの高さ0.8、最低表示行数2位で使用しています。  

* 番組用、予定チューナーの描画方法の変更  
  フォントのサイズではなく高さを基準に計算するので、最低表行数が2行なのに  
  1行しか表示されないとかが無くなります。が、メイリオの場合行間が多めになります。  

* 並び替えのあるリストボックスでマウスのダブルクリックによる追加・削除機能  
  tkntrec版にも採用され整理されたので、逆採用させていただきました。  

* ネットワーク接続時、サーバー側のパスをUNCパスに変換出来る場合は直接再生できます。  
  niisaka氏からのcherry-pickです。

* 予約簡易表示  
  nekopanda氏版をベースに ListView による表示カラムのカスタマイズ機能、  
  タイトルバー非表示機能、最背面表示機能、録画開始までの時間による背景色の色分け、  
  録画中のプログレス表示機能の追加を行っています。

* キーワード予約画面に録画済みタブの追加  
  nekopanda氏版からのmergeです。

* 録画保存フォルダで空き容量をグラフ表示  

* 設定ダイアログでリサイズによるレイアウトを動的変更対応  

* Windowsサービスタブ復活  
  xtne6f氏、tkntrec氏版では削除されましたが、サービス開始ボタン追加とともに復活させました。  
  EpgTimer.exeを管理者権限で起動しなくても操作できるように、外部コマンド呼び出しに変更しました。  

* HTTPサーバー設定タブ追加  
  Material WebUIを使うための初期設定を支援する画面の追加。  
  必要なDLLチェックはしますが、32bit/64bitの区別していないので間違えないように用意してください。  

* 録画タグ機能  
  録画の設定で録画タグが使えるようになります (録画タグにはタブ文字を含めないでください)  
  録画タグの内容はバッチファイル内のマクロ $RecTag$ で参照できます  
  必ずEDCBの設定のバックアップを取ってから使用してください  
  録画タグを使用すると Setting\Reserve.txt に情報が記録され他のバージョンとは互換性が無くなります  
  (テキストエディタで録画タグ情報を取り除いて元に戻すことは可能です)  

* 使用予定チューナーの表示  
  予約一覧/予約簡易表示に使用予定チューナーの表示機能を追加。  

* カスタム番組表での同一TSIDの結合表示のサポート機能  
  SIDの昇順・逆順ソート機能を追加。  
