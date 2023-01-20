const path = require('path');

const MiniCssExtractPlugin = require('mini-css-extract-plugin');

module.exports = {
  mode: "production",
  entry: {
    comments: './src/comments.ts',
    revisions: './src/revisions.ts',
    fileInput: './src/file-input.ts',
    review: './src/review.ts',
    index: './src/index.ts',
    userProfile: './src/user-profile.ts',
    conversation: './css/pages/conversation.scss',
    delete: './css/pages/delete.scss',
    index: './css/pages/index.scss',
    legacyReview: './css/pages/legacy-review.scss',
    profile: './css/pages/profile.scss',
    requestedReviews: './css/pages/requested-reviews.scss',
    review: './css/pages/review.scss',
    revisions: './css/pages/revisions.scss',
    samples: './css/pages/samples.scss',
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
