mergeInto(LibraryManager.library, {

  Hello: function () {
    window.alert("Hello, world!");
  },
  
  Initialization: function () {
    if(window.unityInstance){
       console.log('Initialized');
    }
  },
  
  SendScore: function (score, game) {
    // score: Integer
    // game: Integer
    console.log({score, game});
     if(window.unityInstance){
        window.parent.postMessage({
                    type: 'SEND_SCORE',
                    score: score,
                    game: game
                }, "*");
        console.log('Score message sent to parent');
    }
  }
});