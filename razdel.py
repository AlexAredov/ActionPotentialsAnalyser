import matplotlib.pyplot as plt
from scipy.signal import argrelextrema
import numpy as np

def flat(x, y, n):
    x1 = []
    y1 = []
    for i in range(0,len(x),n):
        x1.append(x[i])
        y1.append(y[i])
    dat = [x1, y1]
    return dat

file = open('./20.txt')
x_t = []
y_t = []
for i in file:
    x_temp = float(i.split('\t')[0].replace('\n', '').replace(',', '.'))
    y_temp = float(i.split('\t')[1].replace('\n', '').replace(',', '.'))
    x_t.append(x_temp)
    y_t.append(y_temp)
plt.plot(x_t, y_t)



xy = np.array([x_t, y_t])

dff = flat(x_t, y_t, 256)

plt.plot(dff[0], dff[1])

dff_t = np.array(dff)

ind = argrelextrema(dff_t, np.less, axis=1)

print(ind)

for i in range(len(ind[1])):
    print(ind[1][i])
    plt.scatter(dff[0][ind[1][i]], dff[1][ind[1][i]], color='green', s=40, marker='o')

plt.show()
