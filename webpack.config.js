var path = require("path");

var babelOptions = {
    presets: [
        ["@babel/preset-env", {
            "targets": {
                "node": true,
            },
        }]
    ],
};

console.log("Bundling function...");

module.exports = {
  mode: "development",
//   mode: "production",
  target: "node",
  node: {
    __dirname: false,
    __filename: false,
  },
  entry: './src/Ionide.FSharp.fsproj',
  output: {
    path: path.join(__dirname, "./release"),
    filename: 'Extension.js',
    libraryTarget: 'commonjs'
  },
  plugins: [ ],
  module: {
    rules: [{
        test: /\.fs(x|proj)?$/,
        use: {
          loader: "fable-loader",
          options: {
            babel: babelOptions,
            define: [] 
          }
        }
      },
      {
        test: /\.js$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: babelOptions
        },
      }
    ]
  },
  externals: {
    // Who came first the host or the plugin ?
    "vscode": "commonjs vscode",

    // Optional dependencies of ws
    "utf-8-validate": "commonjs utf-8-validate",
    "bufferutil": "commonjs bufferutil"
  }
};
