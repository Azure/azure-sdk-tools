const path = require('path');

const MiniCssExtractPlugin = require('mini-css-extract-plugin');

module.exports = {
  mode: "production",
  entry: {
    comments: './src/comments.ts',
    revisions: './src/revisions.ts',
    fileInput: './src/file-input.ts',
    review: './src/review.ts',
    reviews: './src/reviews.ts',
    userProfile: './src/user-profile.ts',
    site: './css/site.scss',
    c: './css/c.scss',
    cplusplus: './css/cplusplus.scss',
    csharp: './css/csharp.scss',
    go: './css/go.scss',
    java: './css/java.scss',
    javascript: './css/javascript.scss',
    json: './css/json.scss',
    kotlin: './css/kotlin.scss',
    python: './css/python.scss',
    swagger: './css/swagger.scss',
    swift: './css/swift.scss',
    xml: './css/xml.scss',
    usagesample: './css/usageSample.scss'
  },
  devtool: 'source-map',
  module: {
    rules: [
      {
        test: /\.s[ac]ss$/i,
        use: [
          {
            loader: MiniCssExtractPlugin.loader
          },
          {
            loader: 'css-loader',
            options: {
              sourceMap: true,
            },
          },
          {
            loader: 'sass-loader',
            options: {
              sourceMap: true,
            },
          },
        ],
      },
      {
        test: /\.ts?$/,
        use: 'ts-loader',
        exclude: /node_modules/,
      },
    ]
  },
  plugins: [
    new MiniCssExtractPlugin({
      filename: '[name].css'
    }),
  ],
  resolve: {
    extensions: [ '.tsx', '.ts', '.js' ],
  },
  output: {
    filename: '[name].js',
    path: path.resolve(__dirname, '../wwwroot'),
  },
}
