#importing libs
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from math import sqrt

#grid
plt.style.use('seaborn-whitegrid')

#the function to found the nearest value for this
def nearest_value(items_x, items_y, value_x, value_y):
    l = []
    for i in range(len(items_x)):
        l.append(sqrt((value_x - items_x[i])**2 + (value_y - items_y[i])**2))
    return(l.index(min(l)))

#function to do graph smoother
def flat(x, y, n):
    x1 = []
    y1 = []
    for i in range(0,len(x),n):
        x1.append(x[i])
        y1.append(y[i])
    dat = [x1, y1]
    return dat

def line(x1, y1, x2, y2):
    k = (y1 - y2) / (x1 - x2)
    b = y2 - k*x2
    print(" y = %.2f*x + %.2f" % (k, b))
    return [k, b]

#open file
file = open('./17.txt')
x_t = []
y_t = []
for i in file:
    x_temp = float(i.split('\t')[0].replace('\n', '').replace(',', '.'))
    y_temp = float(i.split('\t')[1].replace('\n', '').replace(',', '.'))
    x_t.append(x_temp)
    y_t.append(y_temp)

#writing just the right values
x = []
y = []
for i in range(len(x_t)):
    if (x_t[i] < x_t[y_t.index(max(y_t))]) and (y_t[i] < min(y_t)/2):
        x.append(x_t[i])
        y.append(y_t[i])
dat = {'x': x, 'y': y}
datafr = pd.DataFrame(data=dat)

#first flatting (the big one)
l = 32
dff = flat(x, y, 32)
ma = nearest_value(dff[0], dff[1], x[y.index(max(y))], min(y))

#number of dots to the left and right of the center
o = 10

#reduce flatting
while ma + o >= len(dff[0]):
    l = l//2
    dff = flat(x, y, l)
    ma = nearest_value(dff[0], dff[1], x[y.index(max(y))], min(y))
while dff[1][ma+o] - dff[1][ma] >= 0.003:
    o -= 1
    ma = nearest_value(dff[0], dff[1], x[y.index(max(y))], min(y))

#set the size of graph
f = plt.figure()
f.set_figwidth(5*2*max(dff[0])/abs(min(dff[1])))
f.set_figheight(5)

#the number of flatting
print(l)

#the dot (nearest to center)
plt.scatter(x[y.index(max(y))], min(y), color='orange', s=40, marker='o')

#other dots
plt.scatter(dff[0][ma], dff[1][ma], color='green', s=40, marker='o')
plt.scatter(dff[0][ma+o], dff[1][ma+o], color='green', s=40, marker='o')
plt.scatter(dff[0][ma-o], dff[1][ma-o], color='green', s=40, marker='o')

plt.scatter(min(dff[0]), min(dff[1]), color='blue', s=40, marker='o')
plt.scatter(max(dff[0]), max(dff[1]), color='blue', s=40, marker='o')

kb1 = line(min(dff[0]), min(dff[1]), dff[0][ma-o], dff[1][ma-o])
kb2 = line(max(dff[0]), max(dff[1]), dff[0][ma+o], dff[1][ma+o])

plt.axis([min(dff[0])-0.001,max(dff[0]),min(dff[1]),max(dff[1])])

x = np.arange(min(dff[0]), max(dff[0]), dff[0][1]-dff[0][0])
plt.plot(x, kb1[0]*x+kb1[1])
plt.plot(x, kb2[0]*x+kb2[1])

tg = (kb2[0] - kb1[0])/(1 + kb2[0]*kb1[0])
print(180 - np.arctan(tg) * 57.2958)

print(f'1 = {kb1[0]}')
print(f'2 = {kb2[0]}')

#drauing the rigth and flat graphes
plt.plot(datafr.x, datafr.y)
plt.plot(dff[0], dff[1])

plt.show()