「変数」に Rigidbody や AudioSource などのコンポーネントの型の変数を選び、「取り出すもと」にオブジェクトを入れると、その型のコンポーネントが変数に入ります。「探す場所」を「子も探す」にすると子のオブジェクトまで、「親も探す」にすると親のオブジェクトまで探します。どちらも、最初に探すのは自分自身です。

置いてあるオブジェクトなら、ほかのアクションの欄で直接選べます。このアクションが役に立つのは、実行中に分かったオブジェクトを扱うときです。たとえば「物体が範囲に入ったとき」の入ってきた物や、「Udon の機能を呼ぶ」で見つけたオブジェクトから、Rigidbody を取り出して動かせます。

見つからなかったときは、変数が空（null）になります。取り出したあとに使うアクションは、空のときは何もしません。

公式の説明: [GetComponent（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.GetComponent.html)、[GetComponentInChildren（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.GetComponentInChildren.html)、[GetComponentInParent（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.GetComponentInParent.html)
