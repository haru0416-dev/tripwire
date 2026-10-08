ワールドに置いた VRC Contact Receiver に、Contact Sender が触れ始めたときに起きます。イベントは Receiver と同じオブジェクトの UdonBehaviour にしか届きません。Tripwire Trigger は Receiver と同じオブジェクトに付けてください。

触れたと判定されるには、Sender と Receiver の Collision Tags に同じ文字が 1 つ以上必要です。タグは大文字と小文字を区別し、1 つの Contact に 16 個までです。人型のアバターには、Head や Hand などのタグが付いた Sender が自動で作られます。手で押すボタンなら、Receiver のタグに Hand や Finger を入れ、Content Types でアバターを選びます。

「コンタクトの情報」には、触れた Sender（contactSender）と触れた位置（contactPoint）が入っています。触れた速さ（enterVelocity）と、共通するタグ（matchingTags）もあります。誰のアバターかを知る手がかりは、contactSender の player です。ワールドの Sender なら player は空です。中身は「Udon の機能を呼ぶ（Udon API）」で、この値を対象にして読みます。

contactSender の中身を使う前に、isValid がオンかを確かめるよう公式の説明にあります。Contact の形は、半径 3 m、幅・高さ・奥行き 6 m までです。

公式の説明: [Contacts（VRChat）](https://creators.vrchat.com/common-components/contacts/)、[Built-In Contact Tags（VRChat）](https://creators.vrchat.com/common-components/contacts/built-in-contact-tags/)
